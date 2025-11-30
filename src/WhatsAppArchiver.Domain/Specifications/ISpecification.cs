namespace WhatsAppArchiver.Domain.Specifications;

/// <summary>
/// Represents a specification pattern interface for filtering domain objects.
/// </summary>
/// <typeparam name="T">The type of object to evaluate against the specification.</typeparam>
/// <remarks>
/// The Specification pattern encapsulates business rules that determine whether
/// an object satisfies certain criteria. This pattern promotes reusability and
/// composability of business rules.
/// </remarks>
public interface ISpecification<in T>
{
    /// <summary>
    /// Determines whether the specified candidate satisfies the specification.
    /// </summary>
    /// <param name="candidate">The object to evaluate.</param>
    /// <returns>True if the candidate satisfies the specification; otherwise, false.</returns>
    bool IsSatisfiedBy(T candidate);
}
