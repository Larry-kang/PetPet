//------------------------------------------------------------------------------
// <auto-generated>
//     這個程式碼是由範本產生。
//
//     對這個檔案進行手動變更可能導致您的應用程式產生未預期的行為。
//     如果重新產生程式碼，將會覆寫對這個檔案的手動變更。
// </auto-generated>
//------------------------------------------------------------------------------

namespace PetPet.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    public partial class Member
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Member()
        {
            this.Friend = new HashSet<Friend>();
            this.Like_record = new HashSet<Like_record>();
            this.Mail = new HashSet<Mail>();
            this.Messenger = new HashSet<Messenger>();
            this.Pet = new HashSet<Pet>();
            this.Post = new HashSet<Post>();
            this.Report = new HashSet<Report>();
        }
        [DisplayName("信箱")]
        public string Email { get; set; }
        public string Pwd { get; set; }
        [DisplayName("姓名")]
        public string Name { get; set; }
        [DisplayName("生日")]
        public System.DateTime Birthday { get; set; }
        [DisplayName("性別")]
        public bool Gender { get; set; }
        [DisplayName("電話")]
        public string Phone { get; set; }
        public int City_no { get; set; }
        public string Mem_photo { get; set; }
        public bool Enable { get; set; }
        public bool Match_Enable { get; set; }
    
        public virtual City_list City_list { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Friend> Friend { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Like_record> Like_record { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Mail> Mail { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Messenger> Messenger { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Pet> Pet { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Post> Post { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Report> Report { get; set; }
    }
}
