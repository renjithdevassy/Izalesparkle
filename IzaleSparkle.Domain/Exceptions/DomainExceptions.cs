namespace IzaleSparkle.Domain.Exceptions;

public class DomainException(string message) : Exception(message);
public class NotFoundException(string entity, object key)
    : DomainException($"{entity} with key '{key}' was not found.");
public class ValidationException(string message) : DomainException(message);
public class BusinessRuleException(string rule, string message)
    : DomainException($"Business rule '{rule}' violated: {message}");
