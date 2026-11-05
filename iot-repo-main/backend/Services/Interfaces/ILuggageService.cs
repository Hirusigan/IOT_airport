namespace Luggage;
public interface ILuggageService { Task<LuggageDTO?> Get(string id); }
