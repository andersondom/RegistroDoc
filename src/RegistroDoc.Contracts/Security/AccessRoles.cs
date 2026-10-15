namespace RegistroDoc.Contracts.Security;

public static class AccessRoles
{
    public const string Administrador = "Administrador";
    public const string Operador = "Operador";
    public const string Consulta = "Consulta";
    public const string Leitura = Administrador + "," + Operador + "," + Consulta;
    public const string Importacao = Administrador + "," + Operador;
    public static readonly string[] Todos = [Administrador, Operador, Consulta];
}
