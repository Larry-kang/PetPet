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

    public partial class News
    {
        public int News_no { get; set; }
        public int Admin_no { get; set; }
        [DisplayName("主旨")]
        public string N_tital { get; set; }
        [DisplayName("內容")]
        public string N_content { get; set; }
        [DisplayName("照片")]
        public string N_photo { get; set; }
        [DisplayName("張貼時間")]
        public System.DateTime N_post_time { get; set; }
        [DisplayName("有效期限")]
        public System.DateTime N_post_deadline { get; set; }
    
        public virtual Admin Admin { get; set; }
    }
}
