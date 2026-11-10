# Contrato de design — SDKs Almztech

Este documento é a fonte da verdade compartilhada pelas 6 implementações de SDK (.NET, Node/TypeScript, PHP, Java, Python, Ruby). Nenhuma implementação de linguagem deve divergir do que está aqui sem que este documento seja atualizado primeiro. Onde uma linguagem tiver uma razão idiomática forte para divergir (ex. nomenclatura de métodos), a *semântica* descrita aqui continua obrigatória.

Este contrato cobre o escopo v1: **Núcleo NFSe + Emitente** (autenticação, Emitir/Consultar/Cancelar nota, download XML/PDF, listagem/filtros, CRUD+teste de Webhook, municípios homologados, CRUD de Emitente + upload de certificado). Cobrança/Plano está fora de escopo.

## 0. Comportamento da API

Pontos conferidos nas respostas reais da API, que todo SDK precisa respeitar.

- **O JSON é camelCase**, não PascalCase: `{ "protocolo": "...", "status": "PROCESSADO_COM_SUCESSO", "notas": [...] }`. Todo (de)serializador de cada SDK deve usar camelCase como convenção de wire format.
- **Erros de negócio** (`Inconsistencia`) vêm como HTTP **422** com corpo `{ "mensagem": "..." }` (confirmado: `GET /NFSe/Consultar/999999` → 422 `{"mensagem":"Nota de código [999999] não encontrada."}`). **Não é 400**, mesmo nos endpoints em que a documentação cita 400.
- **401 sem token**: `{"mensagem":"Token não informado"}`. Auth, token revogado e licença inválida para emitir em produção compartilham o mesmo status 401, diferenciáveis só pela mensagem em texto livre. Uma **chave malformada** pode voltar como **500** em vez de 401; o SDK repassa o que vier.
- **429** (rate limit, desde out/2026): corpo `{ "mensagem" }` mais o header `Retry-After` em segundos (a janela inteira, conservadora). Limite por usuário e por IP, com política própria em `NFSe/Emitir`. Não confundir com a franquia de notas do plano, que é `Inconsistencia` (422).
- **400**: só quando a API recusa o formato do corpo (tipo errado num campo, `null` num campo numérico obrigatório). Vem como ProblemDetails, com os erros por campo em `errors`. Por isso o SDK **omite nulos** na serialização: muitos campos numéricos da API não aceitam `null`, e o valor padrão (0) é o que ela espera quando o campo não se aplica.
- **Respostas que não são o DTO de entrada**: `Emitente/Cadastrar` e `Atualizar` devolvem `{ mensagem, cpfCnpj, codigo }`; `UploadCertificado` devolve `{ mensagem, chaveCertificado, vencimento, aviso }`; `CoberturaNacional/MunicipiosHomologados` devolve `[{ id: <código IBGE, número>, text }]`.
- **Campos sensíveis vêm em claro nas respostas de leitura**, não só nos requests de escrita: `GET /Emitente/Consultar` retorna `senhaCertificado` e `senhaPrefeitura` em texto puro no corpo da resposta (conferido nas respostas reais). Isso significa que a sanitização de log do SDK (seção 6) precisa cobrir **response bodies**, não só request bodies.

## 1. Estrutura de client / recursos

Um client raiz por linguagem (`AlmztechClient`) expõe sub-clients por recurso, organizados pelo modelo mental do parceiro, não pela organização das rotas da API:

| Sub-client | Rotas da API | Métodos |
|---|---|---|
| `notasFiscais` | `NFSe/*` | `emitir`, `aguardarConclusao` (helper, seção 4), `consultarProtocolo`, `consultarPorCodigo`, `consultarPorNumero`, `consultarLista`, `consultarListaPorTermo`, `consultarComFiltros`, `reconsultar`, `reconsultarPorChaveAcesso`, `cancelar`, `baixarXml`, `baixarPdf`, `baixarModeloPlanilha`, `importarPlanilha` |
| `webhooks` | `Emitente/Webhook*` — exposto como client irmão de `emitentes`, não aninhado, porque webhook é um conceito próprio no modelo mental do parceiro | `obter`, `listarPorEmitente`, `criar`, `atualizar`, `excluir`, `testar` |
| `emitentes` | `Emitente/*` | `consultar`, `consultarPorCodigo`, `consultarPorCpfCnpj`, `consultarPorTermo`, `cadastrar`, `atualizar`, `excluir`, `uploadCertificado` |
| `municipios` | `CoberturaNacional/MunicipiosHomologados` | `homologados(codigoIbge?, termoDeBusca?)` — sem nenhum dos dois a API devolve lista vazia |

**`emitentes.atualizar` grava todos os campos do corpo**, inclusive os padrões fiscais (`naturezaOperacaoPadrao`, `serieRpsPadrao`, `aliquotaIssPadrao`...), que a API aplica às notas importadas por planilha. Só senhas, chave/vencimento do certificado, controle automático e sequências de RPS mantêm o valor gravado quando vêm vazios. Documentar em cada SDK: atualizar a partir do emitente consultado, nunca de um objeto novo.

**Importação por planilha** (`importarPlanilha`): aceita path/bytes/stream do `.xlsx` no formato de `baixarModeloPlanilha` e converte para Base64 (`arquivoBase64`); devolve um protocolo por lote e os erros por linha. Exige o controle automático de RPS no emitente. Mesma regra de retry de `emitir` (nenhum).

## 2. Configuração / autenticação do client

Construtor/factory único, recebendo:

- `apiKey` (obrigatório) → header **`x-api-key`** em toda chamada. Não usar `Authorization: Bearer`, apesar de aceito pela API, para evitar reescrita por proxies/gateways corporativos que tratam esse header de forma especial.
- `timeout` (default ~30s de rede; não confundir com o tempo de processamento fiscal assíncrono de `Emitir`/`Cancelar`, que é tratado pelo helper de polling, seção 4).
- `retryPolicy` opcional (seção 3).
- Envio HTTP injetável — permite o parceiro plugar seu próprio client HTTP/proxy corporativo. **Nenhum SDK tem dependência de runtime** (decisão de out/2026: um ERP não deve herdar conflito de versão por causa do SDK), então o padrão é o HTTP nativo de cada plataforma e o ponto de injeção é uma interface mínima: C# `HttpClient` externo; Node `fetch`; Python `envio_http(metodo, url, headers, corpo, timeout) -> (status, headers, corpo)` sobre `urllib`; Java `EnvioHttp` sobre `java.net.http`; PHP `EnvioHttp` sobre streams (quem quiser cURL/Guzzle implementa a interface); Ruby qualquer objeto com `call` sobre `net/http`. Nos dinâmicos (Python, PHP, Ruby) os dados são dicionários/arrays/hashes no formato do JSON; no Java, modelos com campos públicos e um JSON próprio.
- `logger`/hook de log opcional, sempre passando por sanitização (seção 6), nunca opcional.

**O fluxo de obtenção da API key fica fora do `AlmztechClient`**: a chave está em "Primeiros passos" no Portal, ou sai de `POST /Usuario/Autenticar` (login/senha) → `GET /Usuario/ApiKey`. Não é um JWT: é um token cifrado e não expirável, e o mesmo usuário recebe sempre a mesma chave. É um passo único de setup, não uma operação de runtime — documentar como tal, para não incentivar o parceiro a guardar login/senha em produção. **Revogação existe só no Portal** (Minha conta → Segurança, `POST /Usuario/ApiKey/Revogar`, que exige a sessão do navegador e a senha): ela troca a chave e invalida a anterior, que passa a receber 401 "Token revogado". O SDK não expõe a revogação — quem tem só a chave não pode trocá-la, de propósito.

`enviarParaProducao` é um parâmetro **por chamada** de `emitir()`/`cancelar()`, nunca de configuração do client — é uma característica do lote de notas, não da sessão.

## 3. Hierarquia de exceções

```
AlmztechException                         (base)
├── AlmztechApiException                  (statusCode + mensagem crua da API, sempre presente — não há errorCode estruturado)
│   ├── AlmztechValidationException       (422 — Inconsistencia; a mais comum na prática)
│   ├── AlmztechAuthenticationException   (401 — token ausente/inválido OU licença inválida; API não diferencia estruturalmente, só a mensagem muda)
│   ├── AlmztechAuthorizationException    (403 — operação não permitida para o perfil do usuário)
│   ├── AlmztechRateLimitException        (429 — carrega o Retry-After)
│   └── AlmztechServerException           (500 — mensagem pode ser genérica em produção)
├── AlmztechNetworkException              (falha de rede/timeout/DNS antes de resposta HTTP)
├── AlmztechPollingTimeoutException       (estourou o timeout do helper de polling — carrega o último ProtocoloResponse conhecido)
└── AlmztechWebhookParsingException       (payload de webhook recebido não reconhecido)
```

`AlmztechValidationException` cobre também o 400 do model binding (seção 0), com a mensagem montada a partir de `errors` e o status original preservado.

Toda exceção de API carrega a mensagem crua (não há `errorCode` machine-readable na API hoje — não tentar heurística de parsing de string para sub-classificar além do que está aqui).

## 4. Retry / backoff e helper de polling

**Regra por idempotência real, não por verbo HTTP:**

| Operação | Retry automático |
|---|---|
| Todo `GET` (`consultar*`, `reconsultar`, downloads), `POST NFSe/ConsultarComFiltros` e `POST NFSe/Reconsultar/ChaveAcesso` (semanticamente leitura) | Sim — backoff exponencial + jitter, default 3 tentativas, só em timeout de rede/erro de conexão/5xx (nunca em 4xx, **inclusive 429**: o `Retry-After` é de até 60 s, longo demais para um retry transparente — o parceiro decide pela exceção) |
| `POST NFSe/Emitir` e `POST NFSe/ImportarPlanilha` | **Não**, por padrão. Evita duplicar emissão fiscal. A API não tem idempotency key nativa — nem retry manual do parceiro é garantidamente seguro; documentar isso |
| `POST NFSe/Cancelar` | **Não**, por padrão (mesma cautela; configurável via opção explícita) |
| CRUD de Webhook/Emitente/certificado | Não — mutações de baixo volume, não claramente idempotentes (ex. `POST Webhook` duplicado cria dois registros, a API não valida duplicidade por URL) |

**Helper de polling** — `notasFiscais.aguardarConclusao(protocolo, opções)`:

```
opções = { intervaloInicial: 2s, backoffMultiplicador: 1.5, intervaloMaximo: 30s, timeoutTotal: 5min, onTentativa?: callback }
```

- Estados terminais: `PROCESSADO_COM_SUCESSO` (retorna), `REJEITADO`/`ERRO_INTERNO` (lançam exceção tipada com a `mensagem` — nunca retornar silenciosamente um objeto "com erro" que o parceiro possa ignorar), `CANCELADO` (retorna, contexto de cancelamento).
- Não-terminais: `EM_FILA`, `EM_PROCESSAMENTO`, `PENDENTE_PREFEITURA` — continuam.
- Estourar `timeoutTotal` → `AlmztechPollingTimeoutException` com o último `ProtocoloResponse` conhecido (parceiro pode continuar polling manualmente).
- É complementar ao webhook, não substituto — documentar os dois caminhos.

## 5. Helper de parsing de webhook recebido

`AlmztechWebhook.parse(corpoJsonCru)` → evento tipado. Payload recebido = mesmo schema `ProtocoloResponse` do polling. Classificação (a mesma regra da documentação de webhooks da API):

1. `notas` presente e não vazio → **Emissão**.
2. Senão `dadosCancelamento` presente → **Cancelamento**.
3. Senão `status == "REJEITADO"` → **Rejeição** (motivo em `mensagem`).
4. Senão → `AlmztechWebhookParsingException`.

**Documentar em destaque, sem exceção**: não há verificação de autenticidade/origem do payload (sem HMAC/assinatura hoje). O helper faz só parsing/tipagem — proteger o endpoint receptor (allowlist de IP, token secreto na própria URL, mTLS) é responsabilidade do parceiro. Não sugerir ou implementar uma "verificação" que dê falsa sensação de segurança.

## 6. Sanitização de log

Blocklist central, nunca logada em claro por nenhum log automático do SDK (nem em modo debug/verbose) — cobrindo **tanto request quanto response bodies** (achado da seção 0: `senhaCertificado`/`senhaPrefeitura` vêm em claro em respostas de leitura, não só em escritas):

- `apiKey` / header `x-api-key` completo (mostrar no máximo os últimos 4 caracteres).
- `senha`, `senhaCertificado`, `senhaPrefeitura` (em qualquer DTO de Emitente, request ou response).
- `base64` do certificado (`uploadCertificado`) — nunca logar, nem truncado.
- `login`/`senha` do fluxo de setup de API key, se algum exemplo/utilitário do SDK vier a tocar nisso.

Implementar como interceptador central (`sanitizeForLog(payload)`) antes de qualquer log automático — nunca depender de lembrar de sanitizar em cada call site. Cobrir com testes de regressão usando os fixtures (`fixtures/`).

## 7. Filtros e paginação (`consultarComFiltros`)

Builder tipado, não strings mágicas, com os operadores que a API aceita:

- **Operadores**: `Igual, Diferente, MaiorQue, MaiorOuIgual, MenorQue, MenorOuIgual, Entre, Contem, IniciaCom, TerminaCom`.
- **Campos aceitos**: `protocolo`, `codigoNota`, `nomeEmitente`, `cpfCnpjEmitente`, `numeroRps`, `serieRps`, `loteRps`, `valorTotal`, `dataEmissao`, `status` — emitir sempre o valor canônico, mesmo a API sendo case-insensitive no servidor. Campo desconhecido ou valor vazio é **ignorado** (não filtra, não dá erro).
- **Valor é sempre texto** no wire format: datas `yyyy-MM-dd`, decimais com ponto. `Entre` usa `valorFinal`. `status` aceita vários separados por `|` (`EMITIDA|CANCELADA`). Oferecer construtores que formatem isso (`Periodo`, `ComStatus`, `EntreValores`).
- `ambiente` é obrigatório (`PRD`/`HML`, o prefixo do protocolo); `filtroRapido` é a busca livre.
- Resultado estruturado (não dictionary solto): `notas`, `paginaAtual`, `totalDeRegistros` + os 6 contadores por status (`totalEmProcessamento`, `totalEmitidas`, `totalCanceladas`, `totalDenegadas`, `totalRejeitadas`, `totalPendentes`) + `contagensPorAba` (por status, ignorando o próprio filtro de status, com a chave `all`).
- Limite de página: 100 registros (a API reduz pedidos maiores) — o SDK deve respeitar isso e oferecer um iterador de páginas automático (`.paginas()`), poupando o parceiro do loop manual.

## 8. Outras convenções

- **Datas**: `dataEmissao` é uma data pura (`yyyy-MM-dd`, sem hora) — mapear para tipo de data pura (não datetime) idiomático em cada linguagem.
- **Códigos fiscais são números** no corpo de `NFSe/Emitir` (`exigibilidadeISS`, `tipoTributacao`, `municipioIncidencia`, `retemISS` 1 = retido/2 = não retido, `responsavelPelaRetencao`), não booleanos nem strings. `rps.tipo` é string e `rps.status` é número. Os grupos da Reforma Tributária (`ibsCbs`, `pisCofins`, `totaisDosTributos`) são tipados.
- **Schema de emissão**: espelhar o JSON de `NFSe/Emitir` documentado em docs.almztech.com.br (o `Destinatario` não tem e-mail; `pagamentos` são `{ parcela, dataPagamento, valor }`; `subtituicaoRps` identifica a nota substituída por `numeroRPS`/`numeroNota`/`serie`/`tipo`/`chaveNota` mais `codigoMotivo`/`motivo`).
- **Download de XML/PDF**: retornar bytes/stream; não escrever em disco por padrão (evita efeito colateral surpresa); oferecer parâmetro/overload opcional de conveniência para salvar em um caminho.
- **Upload de certificado**: aceitar path/bytes/stream de `.pfx`; o SDK converte para Base64 internamente — nunca expor o encoding manual como etapa obrigatória para o parceiro.
- **Multi-tenant é implícito no token**: nunca expor parâmetro de "tenant"/dono do emitente nas chamadas de listagem.
- **User-Agent** próprio por SDK (`almztech-dotnet-sdk/1.0.0` etc.) em todo request, para permitir telemetria de adoção do lado da Almztech.

## Limitações conhecidas da API

- O webhook não tem assinatura (HMAC) de origem: o helper da seção 5 só faz o parsing.
- Não há `errorCode` estruturado: as exceções se diferenciam pelo status HTTP, e a mensagem é texto livre.
- Não há chave de idempotência: `emitir` e `cancelar` nunca são repetidos automaticamente (seção 4).
