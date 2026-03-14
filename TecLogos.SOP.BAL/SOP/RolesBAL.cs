using TecLogos.SOP.DAL.SOP;
using TecLogos.SOP.DataModel.SOP;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IRolesBAL
    {
        Task<List<Roles>> GetAll();
        Task<Roles?> Get(Guid id);
        Task<Guid> Create(Roles r, Guid user);
        Task<bool> Update(Roles r, Guid user);
        Task<bool> Delete(Guid id, Guid user);
    }
    public class RolesBAL : IRolesBAL
    {
        private readonly IRolesDAL _rolesDAL;

        public RolesBAL(IRolesDAL rolesDAL)
        {
            _rolesDAL = rolesDAL;
        }

        public Task<List<Roles>> GetAll()
            => _rolesDAL.GetAll();

        public Task<Roles?> Get(Guid id)
            => _rolesDAL.GetById(id);

        public Task<Guid> Create(Roles r, Guid u)
            => _rolesDAL.Create(r, u);

        public Task<bool> Update(Roles r, Guid u)
            => _rolesDAL.Update(r, u);

        public Task<bool> Delete(Guid id, Guid u)
            => _rolesDAL.Delete(id, u);
    }
}
