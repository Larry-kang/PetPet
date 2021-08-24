using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PetPet.Models.Interface
{
    public interface IStructrueRepository:IDisposable
    {
        void Create(Member instance);

        void Update(Member instance);

        void Delete(Member instance);

        Member Get(string fEmail);

        IQueryable<Member> GetAll();

        void SaveChanges();
    }
}