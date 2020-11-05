namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    internal interface IRequestDetailBuilder
    {
        RequestBlockDetail GetDetails();
    }
}