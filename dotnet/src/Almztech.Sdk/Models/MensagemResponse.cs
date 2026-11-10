namespace Almztech.Sdk.Models;

/// <summary>Formato de resposta simples <c>{ "mensagem": "..." }</c> usado por alguns endpoints de sucesso (ex. exclusão, teste de webhook).</summary>
public class MensagemResponse
{
    public string Mensagem { get; set; } = default!;
}
