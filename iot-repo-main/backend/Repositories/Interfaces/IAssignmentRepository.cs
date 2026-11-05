namespace Luggage;
public interface IAssignmentRepository : IRepository<Assignment>
{
    Task<Assignment> GetByJourneyAsync(string journeyId);
    Task<IReadOnlyList<Assignment>> GetAllAsync();
    Task<IReadOnlyList<Assignment>> GetByJourneysAsync(string[] journeyIds);
}
