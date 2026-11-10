require_relative "lib/almztech/versao"

Gem::Specification.new do |spec|
  spec.name = "almztech-sdk"
  spec.version = Almztech::VERSAO_GEM
  spec.authors = ["ALMZTECH SOFTWARES LTDA"]
  spec.summary = "SDK oficial Ruby para a API da Almztech"
  spec.description = "Emissão, consulta, reconsulta e cancelamento de NFS-e, importação por planilha, Emitente e Webhook."
  spec.homepage = "https://docs.almztech.com.br"
  spec.license = "MIT"
  spec.required_ruby_version = ">= 3.0"
  spec.files = Dir["lib/**/*.rb", "README.md", "LICENSE"]
  spec.require_paths = ["lib"]
  spec.metadata = {
    "homepage_uri" => "https://docs.almztech.com.br",
    "documentation_uri" => "https://docs.almztech.com.br",
    "source_code_uri" => "https://github.com/Almztech-Softwares/SDKs/tree/main/ruby",
    # Publicar exige MFA na conta do RubyGems (inclusive pela API/CI).
    "rubygems_mfa_required" => "true",
  }
  # Sem dependências de runtime: net/http e json da biblioteca padrão (DESIGN.md seção 2).
end
