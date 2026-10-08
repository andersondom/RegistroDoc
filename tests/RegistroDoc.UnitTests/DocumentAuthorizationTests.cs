using Microsoft.AspNetCore.Authorization;
using RegistroDoc.Api.Controllers;

namespace RegistroDoc.UnitTests;

public sealed class DocumentAuthorizationTests
{
    [Fact]
    public void DocumentControllerRequiresAuthorizedRoles()
    {
        var attributes = typeof(DocumentosController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        var authorization = Assert.Single(attributes);
        Assert.Equal("Administrador,Operador", authorization.Roles);
    }

    [Theory]
    [InlineData("ObterArquivo")]
    [InlineData("Pesquisar")]
    public void DocumentEndpointsCannotAllowAnonymousAccess(string methodName)
    {
        var method = typeof(DocumentosController).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }
}
