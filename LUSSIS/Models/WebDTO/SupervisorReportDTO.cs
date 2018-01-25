﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LUSSIS.Models.WebDTO
{
    public class SupervisorReportDTO
    {
        public List<Supplier> Suppliers { get; set; }
        public List<Category> Categories { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public bool IsChart { get; set; }

        public List<SelectListItem> SelectedSupplier { get; set; }
        public List<SelectListItem> SelectedCategory { get; set; }
        
    }
}