using Secretary.Domain.Entities;

namespace Secretary.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(Account account);
}
