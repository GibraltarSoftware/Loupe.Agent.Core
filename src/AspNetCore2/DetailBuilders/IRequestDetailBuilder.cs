namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    public interface IRequestDetailBuilder
    {
        RequestBlockDetail GetDetails();
    }
}