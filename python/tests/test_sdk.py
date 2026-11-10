"""Testes do SDK Python contra os fixtures compartilhados (fixtures/) e o contrato atual da API."""

from __future__ import annotations

import json
import unittest
from pathlib import Path
from typing import Mapping, Optional

from almztech import (
    AlmztechClient,
    AlmztechErroAutenticacao,
    AlmztechErroAutorizacao,
    AlmztechErroLimiteDeRequisicoes,
    AlmztechErroProtocoloRejeitado,
    AlmztechErroRede,
    AlmztechErroServidor,
    AlmztechErroTempoDePollingEsgotado,
    AlmztechErroValidacao,
    AlmztechErroWebhook,
    OpcoesDeRetry,
    OpcoesDoClient,
    filtros,
    ler_webhook,
    mascarar_api_key,
    mascarar_corpo_json,
)
from almztech.webhook import CANCELAMENTO, EMISSAO, REJEICAO

FIXTURES = Path(__file__).resolve().parents[2] / "fixtures"
EM_FILA = json.dumps({"protocolo": "HML-1", "status": "EM_FILA"})


def fixture(caminho: str) -> str:
    """Payload do fixture como a API o enviaria (sem ``_comentario``; erros guardam em ``body``, listas em ``response``)."""
    dados = json.loads((FIXTURES / caminho).read_text(encoding="utf-8"))
    dados.pop("_comentario", None)
    return json.dumps(dados.get("body", dados.get("response", dados)))


class EnvioFalso:
    """Envio HTTP falso: responde em fila e guarda método, caminho, headers e corpo de cada chamada."""

    def __init__(self):
        self.fila = []
        self.chamadas = []

    def responder(self, status: int, corpo: str = "", headers: Optional[Mapping[str, str]] = None) -> "EnvioFalso":
        self.fila.append((status, dict(headers or {}), corpo.encode("utf-8")))
        return self

    def falhar_rede(self) -> "EnvioFalso":
        self.fila.append(OSError("rede indisponível"))
        return self

    def __call__(self, metodo, url, headers, corpo, timeout):
        self.chamadas.append({"metodo": metodo, "url": url, "headers": dict(headers), "corpo": corpo})
        resposta = self.fila.pop(0)
        if isinstance(resposta, Exception):
            raise resposta
        return resposta

    def corpo_json(self, indice: int = 0):
        return json.loads(self.chamadas[indice]["corpo"])

    def caminho(self, indice: int = 0) -> str:
        return self.chamadas[indice]["url"].replace("https://sdk-tests.local", "")


def criar_client(envio: EnvioFalso, logger=None, api_key: str = "api-key-de-teste") -> AlmztechClient:
    return AlmztechClient(
        api_key,
        OpcoesDoClient(
            base_url="https://sdk-tests.local",
            envio_http=envio,
            logger=logger,
            retry=OpcoesDeRetry(tentativas=3, intervalo_inicial=0.001, intervalo_maximo=0.005),
        ),
    )


LOTE_VAZIO = {"identificacaoEmitente": "1", "notas": [], "enviarParaProducao": False}


class RetryTestes(unittest.TestCase):
    def test_leitura_retenta_em_5xx(self):
        envio = EnvioFalso().responder(500).responder(503).responder(200, EM_FILA)
        self.assertEqual(criar_client(envio).notas_fiscais.consultar_protocolo("HML-1")["status"], "EM_FILA")
        self.assertEqual(len(envio.chamadas), 3)

    def test_leitura_desiste_apos_falhas_de_rede(self):
        envio = EnvioFalso().falhar_rede().falhar_rede().falhar_rede()
        with self.assertRaises(AlmztechErroRede):
            criar_client(envio).notas_fiscais.consultar_protocolo("HML-1")
        self.assertEqual(len(envio.chamadas), 3)

    def test_emitir_cancelar_e_importar_nunca_retentam(self):
        envio = EnvioFalso().responder(500).falhar_rede().responder(500).responder(500)
        client = criar_client(envio)
        with self.assertRaises(AlmztechErroServidor):
            client.notas_fiscais.emitir(LOTE_VAZIO)
        with self.assertRaises(AlmztechErroRede):
            client.notas_fiscais.emitir(LOTE_VAZIO)
        with self.assertRaises(AlmztechErroServidor):
            client.notas_fiscais.cancelar(7)
        with self.assertRaises(AlmztechErroServidor):
            client.notas_fiscais.importar_planilha("1", b"\x01", False)
        self.assertEqual(len(envio.chamadas), 4)

    def test_4xx_nao_retenta(self):
        envio = EnvioFalso().responder(422, fixture("erros/422-validacao.json"))
        with self.assertRaises(AlmztechErroValidacao):
            criar_client(envio).notas_fiscais.consultar_por_codigo(999999)
        self.assertEqual(len(envio.chamadas), 1)


class ErrosTestes(unittest.TestCase):
    def test_401_403_422_tipados_com_a_mensagem(self):
        envio = (
            EnvioFalso()
            .responder(401, fixture("erros/401-sem-token.json"))
            .responder(403, fixture("erros/403-proibido.json"))
            .responder(422, fixture("erros/422-validacao.json"))
        )
        client = criar_client(envio)
        with self.assertRaises(AlmztechErroAutenticacao) as erro:
            client.emitentes.consultar()
        self.assertEqual(erro.exception.mensagem, "Token não informado")
        with self.assertRaises(AlmztechErroAutorizacao):
            client.emitentes.consultar()
        with self.assertRaises(AlmztechErroValidacao) as erro:
            client.emitentes.consultar()
        self.assertEqual(erro.exception.status_code, 422)

    def test_429_com_retry_after_sem_retentar(self):
        envio = EnvioFalso().responder(429, json.dumps({"mensagem": "Limite de requisições excedido."}), {"Retry-After": "60"})
        with self.assertRaises(AlmztechErroLimiteDeRequisicoes) as erro:
            criar_client(envio).notas_fiscais.consultar_protocolo("HML-1")
        self.assertEqual(erro.exception.retry_after, 60.0)
        self.assertEqual(len(envio.chamadas), 1)

    def test_400_do_aspnet_vira_validacao_com_os_campos(self):
        problema = {"title": "One or more validation errors occurred.", "errors": {"notas[0].servicos[0].retemISS": ["Could not convert."]}}
        envio = EnvioFalso().responder(400, json.dumps(problema))
        with self.assertRaises(AlmztechErroValidacao) as erro:
            criar_client(envio).notas_fiscais.emitir(LOTE_VAZIO)
        self.assertEqual(erro.exception.status_code, 400)
        self.assertIn("retemISS", erro.exception.mensagem)


class PollingTestes(unittest.TestCase):
    opcoes = {"intervalo_inicial": 0.001, "intervalo_maximo": 0.002}

    def test_devolve_no_sucesso(self):
        envio = EnvioFalso().responder(200, fixture("protocolo-responses/em-fila.json")).responder(200, fixture("protocolo-responses/emissao-sucesso.json"))
        resultado = criar_client(envio).notas_fiscais.aguardar_conclusao("SBOX-1", **self.opcoes)
        self.assertEqual(resultado["status"], "PROCESSADO_COM_SUCESSO")
        self.assertTrue(resultado["notas"])

    def test_rejeicao_lanca_com_o_protocolo(self):
        envio = EnvioFalso().responder(200, fixture("protocolo-responses/rejeitado.json"))
        with self.assertRaises(AlmztechErroProtocoloRejeitado) as erro:
            criar_client(envio).notas_fiscais.aguardar_conclusao("SBOX-1", **self.opcoes)
        self.assertEqual(erro.exception.protocolo["status"], "REJEITADO")

    def test_estoura_o_tempo_com_o_ultimo_status(self):
        envio = EnvioFalso()
        for _ in range(50):
            envio.responder(200, EM_FILA)
        with self.assertRaises(AlmztechErroTempoDePollingEsgotado) as erro:
            criar_client(envio).notas_fiscais.aguardar_conclusao("HML-1", timeout_total=0.005, **self.opcoes)
        self.assertEqual(erro.exception.ultimo_protocolo["status"], "EM_FILA")


class WebhookTestes(unittest.TestCase):
    def test_classifica_os_tres_eventos(self):
        self.assertEqual(ler_webhook(fixture("webhook-payloads/emissao.json")).tipo, EMISSAO)
        self.assertEqual(ler_webhook(fixture("webhook-payloads/cancelamento.json")).tipo, CANCELAMENTO)
        self.assertEqual(ler_webhook(fixture("webhook-payloads/rejeicao.json")).tipo, REJEICAO)
        self.assertEqual(ler_webhook(json.loads(fixture("webhook-payloads/emissao.json"))).tipo, EMISSAO)

    def test_recusa_vazio_invalido_e_desconhecido(self):
        for corpo in ("", "{nao json", json.dumps({"protocolo": "X", "status": "EM_FILA"})):
            with self.assertRaises(AlmztechErroWebhook):
                ler_webhook(corpo)


class LogTestes(unittest.TestCase):
    def test_mascara_em_qualquer_profundidade(self):
        corpo = json.dumps({"cpfCnpj": "123", "senhaCertificado": "s1", "endereco": {"senhaPrefeitura": "s2"}, "lista": [{"base64": "AAA"}], "arquivoBase64": "UEs="})
        mascarado = mascarar_corpo_json(corpo)
        for segredo in ("s1", "s2", "AAA", "UEs="):
            self.assertNotIn(segredo, mascarado)
        self.assertIn("123", mascarado)
        self.assertEqual(mascarar_corpo_json("não é json"), "<corpo não-JSON omitido da sanitização de log>")
        self.assertEqual(mascarar_api_key("abcdefgh1234"), "***1234")

    def test_logger_nunca_recebe_chave_nem_senha(self):
        linhas = []
        envio = EnvioFalso().responder(200, json.dumps([{"razaoSocial": "X", "cpfCnpj": "1", "senhaCertificado": "segredo"}]))
        criar_client(envio, logger=linhas.append, api_key="chave-secreta-9999").emitentes.consultar()
        tudo = "\n".join(linhas)
        self.assertNotIn("chave-secreta", tudo)
        self.assertNotIn("segredo", tudo)
        self.assertIn("***9999", tudo)


class ContratoTestes(unittest.TestCase):
    def test_emitir_manda_codigos_numericos_omite_nulos_e_manda_headers(self):
        envio = EnvioFalso().responder(200, EM_FILA)
        criar_client(envio).notas_fiscais.emitir(
            {
                "identificacaoEmitente": "12345678000190",
                "enviarParaProducao": False,
                "notas": [
                    {
                        "rps": {"serie": "1", "tipo": "1", "dataEmissao": "2026-10-31", "numeroRPS": None},
                        "destinatario": {"cpfCnpj": "12345678909", "razaoSocial": "Cliente", "endereco": {"codigoIBGEMunicipio": 2611606}},
                        "servicos": [{"codigo": "0105", "descricao": "Teste", "valorUnitario": 100, "retemISS": 2}],
                        "observacao": None,
                    }
                ],
            }
        )
        nota = envio.corpo_json()["notas"][0]
        self.assertEqual(nota["servicos"][0]["retemISS"], 2)
        self.assertNotIn("numeroRPS", nota["rps"])
        self.assertNotIn("observacao", nota)
        headers = envio.chamadas[0]["headers"]
        self.assertEqual(headers["x-api-key"], "api-key-de-teste")
        self.assertTrue(headers["User-Agent"].startswith("almztech-python-sdk/"))

    def test_municipios_id_numerico_e_query(self):
        envio = EnvioFalso().responder(200, json.dumps([{"id": 2611606, "text": "2611606 - Recife"}]))
        municipios = criar_client(envio).municipios.homologados(codigo_ibge=2611606)
        self.assertEqual(municipios[0]["id"], 2611606)
        self.assertEqual(envio.caminho(), "/CoberturaNacional/MunicipiosHomologados?codigo=2611606")

    def test_cadastro_devolve_codigo_e_upload_manda_base64(self):
        envio = (
            EnvioFalso()
            .responder(200, json.dumps({"mensagem": "Emitente cadastrado com sucesso.", "codigo": 42}))
            .responder(200, json.dumps({"mensagem": "ok", "chaveCertificado": "abc.pfx"}))
        )
        client = criar_client(envio)
        self.assertEqual(client.emitentes.cadastrar({"razaoSocial": "Empresa", "cpfCnpj": "1"})["codigo"], 42)
        self.assertEqual(client.emitentes.upload_certificado("1", b"\x01\x02\x03", "senha")["chaveCertificado"], "abc.pfx")
        self.assertEqual(envio.corpo_json(1)["base64"], "AQID")

    def test_importar_planilha_manda_base64(self):
        resultado = {"protocolos": [{"protocolo": "HML-9", "status": "EM_FILA"}], "totalLinhasLidas": 3, "totalNotasValidas": 2, "erros": [{"linha": 4, "mensagem": "CPF inválido"}]}
        envio = EnvioFalso().responder(200, json.dumps(resultado))
        importado = criar_client(envio).notas_fiscais.importar_planilha("1", b"\x01\x02\x03", False)
        self.assertEqual(importado["erros"][0]["linha"], 4)
        self.assertEqual(envio.corpo_json()["arquivoBase64"], "AQID")
        self.assertNotIn("urlWebhook", envio.corpo_json())

    def test_filtros_como_texto(self):
        envio = EnvioFalso().responder(200, json.dumps({"notas": [], "paginaAtual": 1, "totalDeRegistros": 0, "contagensPorAba": {"all": 5}}))
        resultado = criar_client(envio).notas_fiscais.consultar_com_filtros(
            filtros.PRODUCAO,
            [filtros.periodo("2026-10-01", "2026-10-31"), filtros.com_status("EMITIDA", "CANCELADA"), filtros.entre_valores(filtros.CamposFiltro.VALOR_TOTAL, 10.5, 100)],
        )
        self.assertEqual(resultado["contagensPorAba"]["all"], 5)
        corpo = envio.corpo_json()
        self.assertEqual(corpo["ambiente"], "PRD")
        self.assertNotIn("filtroRapido", corpo)
        self.assertEqual(corpo["filtros"][0], {"campo": "dataEmissao", "operador": "Entre", "valor": "2026-10-01", "valorFinal": "2026-10-31"})
        self.assertEqual(corpo["filtros"][1]["valor"], "EMITIDA|CANCELADA")
        self.assertEqual(corpo["filtros"][2]["valor"], "10.5")

    def test_paginas_percorre_ate_o_total(self):
        def pagina(codigos, total):
            return json.dumps({"notas": [{"codigoNota": c} for c in codigos], "paginaAtual": 1, "totalDeRegistros": total})

        envio = EnvioFalso().responder(200, pagina([1, 2], 3)).responder(200, pagina([3], 3))
        codigos = [nota["codigoNota"] for nota in criar_client(envio).notas_fiscais.paginas(filtros.PRODUCAO, limite_da_pagina=2)]
        self.assertEqual(codigos, [1, 2, 3])
        self.assertEqual(envio.corpo_json(1)["pagina"], 2)

    def test_reconsulta_por_chave_e_webhook_com_evento_numerico(self):
        envio = (
            EnvioFalso()
            .responder(200, json.dumps({"codigoNota": 7, "situacao": "CANCELADA", "protocolo": "PRD-1"}))
            .responder(200, json.dumps({"codigo": "g", "evento": 0, "ativo": True, "url": "https://x", "codigoEmitente": 1}))
        )
        client = criar_client(envio)
        self.assertEqual(client.notas_fiscais.reconsultar_por_chave_acesso("NFS26116", True)["situacao"], "CANCELADA")
        self.assertEqual(envio.corpo_json(0), {"enviarParaProducao": True})
        client.webhooks.criar(1, client.webhooks.EMISSAO, "https://x")
        self.assertEqual(envio.corpo_json(1)["evento"], 0)

    def test_lista_do_fixture(self):
        envio = EnvioFalso().responder(200, fixture("nota-fiscal-resumida/lista-notas.json"))
        notas = criar_client(envio).notas_fiscais.consultar_lista(filtros.PRODUCAO)
        self.assertTrue(notas)
        self.assertIsInstance(notas[0]["codigoNota"], int)


if __name__ == "__main__":
    unittest.main()
