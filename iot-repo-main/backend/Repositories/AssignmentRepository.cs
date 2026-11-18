using Microsoft.EntityFrameworkCore;
namespace Luggage;
public class AssignmentRepository(TrackingDbContext db) : BaseRepository<Assignment>(db), IAssignmentRepository
{
    public Task<Assignment> GetByJourneyAsync(string journeyId) => Set.SingleAsync(x => x.JourneyId == journeyId);
    public Task<IReadOnlyList<Assignment>> GetAllAsync() => ListAsync(_ => true);
    public Task<IReadOnlyList<Assignment>> GetByJourneysAsync(string[] journeyIds) => ListAsync(x => journeyIds.Contains(x.JourneyId));
}
