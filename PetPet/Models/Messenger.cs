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
    
    public partial class Messenger
    {
        public int Msg_no { get; set; }
        public int Post_no { get; set; }
        public string Email { get; set; }
        public string Msg_content { get; set; }
        public bool Msg_Enable { get; set; }
        public System.DateTime Mag_time { get; set; }
    
        public virtual Member Member { get; set; }
        public virtual Post Post { get; set; }
    }
}
