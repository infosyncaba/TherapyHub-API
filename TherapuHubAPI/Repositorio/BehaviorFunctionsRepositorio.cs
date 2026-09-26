using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio.IRepositorio;

namespace TherapuHubAPI.Repositorio;

public class BehaviorFunctionsRepositorio : Repository<BehaviorFunctions>, IBehaviorFunctionsRepositorio
{
    public BehaviorFunctionsRepositorio(ContextDB context) : base(context)
    {
    }
}
