using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PetPet.Models;
using PetPet.Models.Interface;

namespace PetPet.Repository
{
    public class StructureRepository : IStructrueRepository, IDisposable
    {
        protected petpetEntities Context
        {
            get;
            private set;
        }

        public StructureRepository()
        {
            this.Context = new petpetEntities();
        }

        public void Create(Member instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }
            else
            {
                Context.Member.Add(instance);
                this.SaveChanges();
            }
        }

        public void Update(Member instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }
            else
            {
                //Context.Entry(instance).State = EntityState.Modified;
                this.SaveChanges();
            }
        }

        public void Delete(Member instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }
            else
            {
                //Context.Entry(instance).State = EntityState.Deleted;
                this.SaveChanges();
            }
        }

        public Member Get(string fEmail)
        {
            return Context.Member.FirstOrDefault(x => x.Email == fEmail);
        }

        public IQueryable<Member> GetAll()
        {
            return Context.Member.OrderBy(x => x.Email);
        }

        public void SaveChanges()
        {
            this.Context.SaveChanges();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Context != null)
                {
                    this.Context.Dispose();
                    this.Context = null;
                }
            }
        }
    }
}