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
    
    public partial class ReportView
    {
        public string Post_Email { get; set; }
        public int Report_no { get; set; }
        public int Expr1 { get; set; }
        public string Expr2 { get; set; }
        public int VType_no { get; set; }
        public string VType_name { get; set; }
        public int Freeze_day { get; set; }
        public int Post_no { get; set; }
        public bool Processing_stsus { get; set; }
        public Nullable<System.DateTime> Unfreeze_date { get; set; }
        public int Report_amount { get; set; }
    }
}