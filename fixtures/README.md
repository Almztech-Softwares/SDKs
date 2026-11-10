# Fixtures de contract test

JSON reais (ou derivados de exemplos reais) usados como fixture compartilhado entre as 6 implementações de SDK, para garantir que todas "entendem" o mesmo formato de dado sem depender de uma API real disponível no CI. Ver `DESIGN.md` seção 0 para o comportamento da API que eles registram.

Arquivos marcados com `"_comentario"` incluem uma nota de proveniência (capturado ao vivo vs. ilustrativo) — remover o campo `_comentario` antes de usar como fixture de asserção estrita em teste automatizado, ele não faz parte do payload real da API.

- `protocolo-responses/` — schema `ProtocoloResponse`, o payload central de polling (`GET NFSe/Consultar/Protocolo/{protocolo}`).
- `webhook-payloads/` — os 3 formatos de payload recebidos via webhook (mesmo schema de `ProtocoloResponse`, classificados por campo presente — ver `DESIGN.md` seção 5).
- `nota-fiscal-resumida/` — listagens de notas (`DtoNotaFiscal`).
- `erros/` — formato de erro por status HTTP (401/403/422).
- `filtros/` — reservado para exemplo de request/response de `POST NFSe/ConsultarComFiltros` (ainda sem exemplo).

Como foram obtidos: a partir de respostas reais da API, com dados de teste fictícios.
