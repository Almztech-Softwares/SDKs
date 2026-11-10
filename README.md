# SDKs Almztech

SDKs oficiais da [Almztech](https://almztech.com.br) para a API de NFS-e, em seis linguagens. Todos cobrem:
- emissão, consulta, reconsulta e cancelamento de notas, inclusive por planilha;
- download do XML e do PDF;
- emitentes e certificados digitais;
- webhooks;
- municípios homologados.

Os seis seguem o mesmo [contrato](DESIGN.md): mesma estrutura de client, mesmos erros tipados, retry só nas
leituras e acompanhamento do protocolo de emissão por polling ou webhook. **Nenhum tem dependência de runtime**:
cada SDK usa só o HTTP e o JSON da própria plataforma.

> Versão beta: a API pública dos SDKs ainda pode mudar até a 1.0.

## Instalação

| Linguagem | Requisito | Pacote | Instalação |
|---|---|---|---|
| [.NET](dotnet) | .NET 8+ | [`Almztech.Sdk`](https://www.nuget.org/packages/Almztech.Sdk) | `dotnet add package Almztech.Sdk --prerelease` |
| [Node.js](nodejs) | Node 18+, TypeScript | [`@almztech/sdk`](https://www.npmjs.com/package/@almztech/sdk) | `npm install @almztech/sdk@beta` |
| [Python](python) | Python 3.9+ | [`almztech-sdk`](https://pypi.org/project/almztech-sdk/) | `pip install --pre almztech-sdk` |
| [PHP](php) | PHP 8.1+ | [`almztech/sdk`](https://packagist.org/packages/almztech/sdk) | `composer require almztech/sdk:^0.2@beta` |
| [Java](java) | Java 11+ | `br.com.almztech:almztech-sdk` | em breve no Maven Central |
| [Ruby](ruby) | Ruby 3.0+ | [`almztech-sdk`](https://rubygems.org/gems/almztech-sdk) | `gem install almztech-sdk --pre` |

O README de cada pasta tem o exemplo de emissão completo, os webhooks, as consultas e a lista de erros.

## Autenticação

Os SDKs usam a **api key** da sua conta, que fica no [Portal](https://portal.almztech.com.br), em Primeiros
passos. Guarde-a na configuração do seu sistema (variável de ambiente, cofre de segredos), nunca no código.
Produção e homologação usam a mesma chave; o ambiente é escolhido a cada emissão.

## Links

- [Documentação da API](https://docs.almztech.com.br)
- [Site da Almztech](https://almztech.com.br)

## Licença

MIT, © 2026 ALMZTECH SOFTWARES LTDA. Veja [LICENSE](LICENSE).
