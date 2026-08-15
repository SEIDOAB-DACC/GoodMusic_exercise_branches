namespace Services;

public interface IAdminService
{
    public Task SeedAsync(int seedCount);
}
