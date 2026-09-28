namespace Trellis.Api.Modules.Billing.Infrastructure.Security;

public interface IPaystackSignatureValidator
{
    Task<bool> IsValidAsync(string body, string? signature);
}
