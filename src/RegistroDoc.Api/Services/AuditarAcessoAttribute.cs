namespace RegistroDoc.Api.Services;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditarAcessoAttribute(string acao) : Attribute
{
    public string Acao { get; } = acao;
}
