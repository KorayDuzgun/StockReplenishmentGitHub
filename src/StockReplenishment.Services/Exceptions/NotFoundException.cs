using System.Net;

namespace StockReplenishment.Services.Exceptions;

/// <summary>Specialised <see cref="BusinessException"/> for missing entities, mapped to HTTP 404.</summary>
public sealed class NotFoundException : BusinessException
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.", HttpStatusCode.NotFound)
    {
    }
}
