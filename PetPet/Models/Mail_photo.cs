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
    
    public partial class Mail_photo
    {
        public int Photo_no { get; set; }
        public int Mail_no { get; set; }
        public string Mail_Photo1 { get; set; }
    
        public virtual Mail Mail { get; set; }
    }
}
