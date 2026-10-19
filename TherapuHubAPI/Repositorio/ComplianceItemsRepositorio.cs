using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio.IRepositorio;

namespace TherapuHubAPI.Repositorio;

public class ComplianceItemsRepositorio : Repository<ComplianceItems>, IComplianceItemsRepositorio
{
    public ComplianceItemsRepositorio(ContextDB context) : base(context)
    {
    }
}
