﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using LUSSIS.Models;
using LUSSIS.Models.WebDTO;
using LUSSIS.Repositories;


namespace LUSSIS.Controllers
{

    public class PurchaseOrdersController : Controller
    {
        private PORepository pr = new PORepository();
        private DisbursementRepository disRepo = new DisbursementRepository();
        private StockAdjustmentRepository stockRepo = new StockAdjustmentRepository();
        private StationeryRepository sr = new StationeryRepository();
        private EmployeeRepository er = new EmployeeRepository();
        private SupplierRepository sur = new SupplierRepository();
        public const double GST_RATE = 0.07;

        // GET: PurchaseOrders
        public ActionResult Index()
        {
            var purchaseOrders = pr.GetAll();
            return View(purchaseOrders.ToList().OrderByDescending(x => x.CreateDate));
        }

        // GET: PurchaseOrders/Details/10005
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PurchaseOrder purchaseOrder = pr.GetById(Convert.ToInt32(id));
            if (purchaseOrder == null)
            {
                return HttpNotFound();
            }
            PurchaseOrderDTO po = new PurchaseOrderDTO(purchaseOrder);

            //ViewBag.PurchaseOrder = po;
            return View(po);
        }


        //GET: PurchaseOrders/Summary
        public ActionResult Summary()
        {
            ViewBag.OutstandingStationeryList = sr.GetOutstandingStationeryByAllSupplier();
            ViewBag.PendingApprovalPOList = pr.GetPOByStatus("pending");
            ViewBag.OrderedPOList = pr.GetPOByStatus("ordered");
            ViewBag.ApprovedPOList = pr.GetPOByStatus("approved");
            return View();
        }


        // GET: PurchaseOrders/Create or PurchaseOrders/Create?supplierId=1
        public ActionResult Create(int? supplierId, string error = null)
        {
            //catch error from redirect
            ViewBag.Error = error;

            PurchaseOrderDTO po = new PurchaseOrderDTO(); //view model
            List<Stationery> stationeries;  //for dropdown list
            int countOfLines = 1; //no of purchase detail lines to show

            if (supplierId == null)
            {
                Supplier selectASupplier = new Supplier();
                selectASupplier.SupplierId = -1;
                selectASupplier.SupplierName = "Select a Supplier";
                ViewBag.Suppliers = sur.GetAll().Concat(new Supplier[] { selectASupplier });
                ViewBag.Supplier = selectASupplier;
                PurchaseOrderDTO nothingToShow = new PurchaseOrderDTO();
                nothingToShow.SupplierId = -1;
                return View(nothingToShow);
            }

            Supplier supplier = sur.GetById(Convert.ToInt32(supplierId));
            po.Supplier = supplier;
            po.SupplierId = supplier.SupplierId;
            po.CreateDate = DateTime.Today;

            //create list of purchase details for outstanding items
            stationeries = sr.GetStationeryBySupplierId(supplierId).ToList();
            foreach (Stationery stationery in stationeries)
            {
                if (stationery.CurrentQty < stationery.ReorderLevel)
                {
                    PurchaseOrderDetailDTO pdetails = new PurchaseOrderDetailDTO();
                    pdetails.OrderQty = stationery.ReorderLevel - stationery.CurrentQty;
                    pdetails.UnitPrice = stationery.UnitPrice(Convert.ToInt32(supplierId));
                    pdetails.ItemNum = stationery.ItemNum;
                    po.PurchaseOrderDetailsDTO.Add(pdetails);
                }
            }
            countOfLines = po.PurchaseOrderDetailsDTO.Count;


            //create empty puchase details so user can add up to 100 line items per PO
            for (int i = countOfLines; i < 100; i++)
            {
                PurchaseOrderDetailDTO pdetails = new PurchaseOrderDetailDTO();
                pdetails.OrderQty = 0;
                pdetails.UnitPrice = stationeries.First().UnitPrice(Convert.ToInt32(supplierId));
                pdetails.ItemNum = stationeries.First().ItemNum;
                po.PurchaseOrderDetailsDTO.Add(pdetails);
            }

            //fill ViewBag to populate stationery dropdown lists
            ViewBag.Stationery = sr.GetStationerySupplierBySupplierId(supplierId).ToList();
            ViewBag.Suppliers = sur.GetAll();
            ViewBag.Supplier = supplier;
            ViewBag.countOfLines = countOfLines;
            ViewBag.GST_RATE = GST_RATE;

            return View(po);
        }


        // POST: PurchaseOrders/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(PurchaseOrderDTO purchaseOrderDTO)
        {
            try
            {
                if (ModelState.IsValid)
                    throw new Exception("IT Error: please contact your administrator");

                //create PO
                PurchaseOrder purchaseOrder = new PurchaseOrder();

                //set PO values
                purchaseOrder.OrderEmpNum = er.GetCurrentUser().EmpNum;
                if (purchaseOrderDTO.CreateDate == null)
                    purchaseOrder.CreateDate = DateTime.Today;
                else
                    purchaseOrder.CreateDate = purchaseOrderDTO.CreateDate;
                purchaseOrder.Status = "pending";
                purchaseOrder.SupplierId = purchaseOrderDTO.SupplierId;

                //set PO detail values
                for (int i = purchaseOrderDTO.PurchaseOrderDetailsDTO.Count - 1; i >= 0; i--)
                {
                    PurchaseOrderDetailDTO pdetail = purchaseOrderDTO.PurchaseOrderDetailsDTO.ElementAt(i);
                    if (pdetail.OrderQty > 0)
                    {
                        PurchaseOrderDetail newPdetail = new PurchaseOrderDetail();
                        newPdetail.ItemNum = pdetail.ItemNum;
                        newPdetail.OrderQty = pdetail.OrderQty;
                        newPdetail.UnitPrice = pdetail.UnitPrice;
                        newPdetail.ReceiveQty = 0;
                        purchaseOrder.PurchaseOrderDetails.Add(newPdetail);
                    }
                    else
                        throw new Exception("Purchase Order was not created, ordered quantity cannot be negative");
                }
                if (purchaseOrder.PurchaseOrderDetails.Count == 0)
                    throw new Exception("Purchase Order was not created, no items found");

                //save to database
                pr.Add(purchaseOrder);

                return RedirectToAction("Summary");
            }
            catch (Exception e)
            {
                return RedirectToAction("Create", new { supplierId = purchaseOrderDTO.SupplierId.ToString(), error = e.Message });
            }
        }


        //GET: PurchaseOrders/Receive?p=10001
        [HttpGet]
        public ActionResult Receive(int? p = null, string error = null)
        {
            //catch error from redirect
            ViewBag.Error = error;

            PurchaseOrderDTO po = null;    //to be put in ViewBag to display
            ReceiveTransDTO receive = new ReceiveTransDTO();    //model to bind data

            if (p == null)
            {
                ViewBag.OrderedPO = pr.GetPOByStatus("ordered");
                return View();
            }

            //populate PO and ReceiveTrans if PO number is given
            po = new PurchaseOrderDTO(pr.GetById(Convert.ToInt32(p)));
            if (po.Status != "ordered")
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            for (int i = 0; i < po.PurchaseOrderDetails.Count; i++)
            {
                ReceiveTransDetail rdetail = new ReceiveTransDetail();
                rdetail.ItemNum = po.PurchaseOrderDetails.Skip(i).First().ItemNum;
                rdetail.Quantity = 0;
                receive.ReceiveTransDetails.Add(rdetail);
            }
            receive.ReceiveDate = DateTime.Today;

            ViewBag.PurchaseOrder = po;
            return View(receive);
        }

        //POST: PurchaseOrders/Receive
        [HttpPost]
        public ActionResult Receive(ReceiveTransDTO receiveModel)
        {
            try
            {
                if (!ModelState.IsValid)
                    throw new Exception("IT Error: please contact your administrator");

                PurchaseOrder po = pr.GetById(Convert.ToInt32(receiveModel.PoNum));
                ReceiveTran receive = receiveModel.ConvertToReceiveTran();
                if (receive.ReceiveDate == null) receive.ReceiveDate = DateTime.Today;
                bool fulfilled = true;

                //check for validity
                int? totalQty = 0;
                foreach (ReceiveTransDetail rdetail in receive.ReceiveTransDetails)
                {
                    totalQty += rdetail.Quantity;
                    if (rdetail.Quantity < 0)
                        throw new Exception("Record not saved, received quantity cannot be negative");
                }
                if (totalQty == 0)
                    throw new Exception("Record not saved, not receipt of goods found");


                //update received quantity in purchase order
                for (int i = po.PurchaseOrderDetails.Count - 1; i >= 0; i--)
                {
                    int receiveQty = Convert.ToInt32(receive.ReceiveTransDetails.ElementAt(i).Quantity);
                    if (receiveQty > 0)
                    {
                        //update po received qty
                        po.PurchaseOrderDetails.ElementAt(i).ReceiveQty += receiveQty;
                        if (po.PurchaseOrderDetails.ElementAt(i).ReceiveQty < po.PurchaseOrderDetails.ElementAt(i).OrderQty)
                            fulfilled = false;

                        //update stationery
                        Stationery s = sr.GetById(po.PurchaseOrderDetails.ElementAt(i).Stationery.ItemNum);
                        s.AverageCost = ((s.AverageCost * s.CurrentQty)
                                        + (receiveQty * po.PurchaseOrderDetails.ElementAt(i).UnitPrice) * (1 + GST_RATE))
                                        / (s.CurrentQty + receiveQty);
                        s.CurrentQty += receiveQty;
                        s.AvailableQty += receiveQty;
                        sr.Update(s);   //persist stationery data here
                    }
                    else if (receiveQty == 0)
                        //keep only the receive transactions details with non-zero quantity
                        receive.ReceiveTransDetails.Remove(receive.ReceiveTransDetails.ElementAt(i));
                }

                //update purchase order and create receive trans
                if (fulfilled) po.Status = "fulfilled";
                po.ReceiveTrans.Add(receive);
                pr.Update(po);

                return RedirectToAction("Summary");
            }
            catch (Exception e)
            {
                return RedirectToAction("Receive", new { p = receiveModel.PoNum.ToString(), error = e.Message });
            }
        }



        //GET: PurchaseOrders/Order?p=10001
        [HttpGet]
        public async Task<ActionResult> Order(int? p = null, string error = null)
        {
            //catch error from redirect
            ViewBag.Error = error;

            PurchaseOrderDTO po = null;
            if (p == null)
            {
                ViewBag.ApprovedPO = pr.GetPOByStatus("approved");
                return View();
            }

            //populate PO DTO if PO number is given
            PurchaseOrder purchaseOrder = await pr.GetByIdAsync(Convert.ToInt32(p));
            if (purchaseOrder.Status != "approved")
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            po = new PurchaseOrderDTO(purchaseOrder);
            po.OrderDate = DateTime.Today;

            return View(po);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Order(PurchaseOrderDTO po)
        {
            try
            {
                if(!ModelState.IsValid)
                    throw new Exception("IT Error: please contact your administrator");
                PurchaseOrder purchaseorder = pr.GetById(po.PoNum);
                purchaseorder.Status = "ordered";
                purchaseorder.OrderDate = po.OrderDate;
                if (po.OrderDate < po.CreateDate)
                    throw new Exception("Record not saved, ordered date cannot be before created date");
                pr.Update(purchaseorder);
                return RedirectToAction("Summary");
            }
            catch (Exception e)
            {
                return RedirectToAction("Order", new { p = po.PoNum.ToString(), error = e.Message });
            }
        }

        public async Task<ActionResult> ViewPendingPOList()
        {

            return View(pr.GetPendingApprovalPODTO());

        }
        [HttpGet]
        public ActionResult ApproveRejectPO(String List, String Status)
        {

            ViewBag.checkList = List;
            ViewBag.status = Status;
            return PartialView("ApproveRejectPO");
        }
        [HttpPost]
        public ActionResult ApproveRejectPO(String checkList, String status, String a)
        {
            String[] list = checkList.Split(',');
            int[] idList = new int[list.Length];
            for (int i = 0; i < idList.Length; i++)
            {
                idList[i] = Int32.Parse(list[i]);
            }
            foreach (int i in idList)
            {
                pr.UpDatePOStatus(i, status);
            }
            return PartialView();
        }
        public async Task<ActionResult> SupervisorDashboard()
        {
            SupervisorDashboardDTO dash = new SupervisorDashboardDTO();

            dash.PendingPOTotalAmount = pr.GetPendingPOTotalAmount();
            dash.PendingPOCount = pr.GetPendingPOCount();
            dash.POTotalAmount = pr.GetPOTotalAmount();
            dash.PendingStockAdjAddQty = stockRepo.GetPendingStockAddQty();
            dash.PendingStockAdjSubtractQty = stockRepo.GetPendingStockSubtractQty();
            dash.PendingStockAdjCount = stockRepo.GetPendingStockCount();
            dash.TotalDisbursementAmount = disRepo.GetDisbursementTotalAmount();
            dash.CharterName = er.GetDepartmentNames();
            dash.CharterValue =er.GetDepartmentValue();
            dash.PieName = sr.GetCategoryList();
            dash.PieValue = sr.GetCategoryPO();
            return View(dash);
        }
      /*  public ActionResult CharterColumn()
        {
            ArrayList xValue = new ArrayList();
            ArrayList yValue = new ArrayList();

            List<double> list = new List<double>();
                List<Department> depList = er.GetDepartmentAll();
              
                foreach (Department e in depList)
                {
                xValue.Add(e.DeptCode);
                yValue.Add(disRepo.GetDisbursementByDepCode(e.DeptCode));

                }
            new System.Web.Helpers.Chart(width: 600, height: 330, theme: ChartTheme.Blue)
                .AddTitle("Chart for ")
                .AddSeries("Default", chartType: "Column", xValue: xValue, yValues: yValue)
                .Write("bmp");

            return null;
        }
        public ActionResult PieChartColumn()
        {
            ArrayList xValue = new ArrayList();
            ArrayList yValue = new ArrayList();

            List<double> list = new List<double>();
           // List<Category> cateList = sr.GetCategoryList();

           /* foreach (Category e in cateList)
            {
                xValue.Add(e.CategoryName);
                yValue.Add(pr.GetPOAmountByCategory(e.CategoryId));

            }
            new System.Web.Helpers.Chart(width: 600, height: 600, theme: ChartTheme.Blue)
                .AddTitle("Chart for ")
                .AddSeries("Default", chartType: "Pie", xValue: xValue, yValues: yValue)
                .Write("bmp");

            return null;
        }*/
        public JsonResult GetPiechartJSON()
        {
            List<String> pileName = sr.GetCategoryList();
            List<double> pileValue = sr.GetCategoryPO();

            return Json(new { ListOne = pileName, ListTwo = pileValue }, JsonRequestBehavior.AllowGet);
        }
        public JsonResult GetBarchartJSON()
        {
            List<String> Name =er.GetDepartmentNames();
            List<double> Value = er.GetDepartmentValue();

            return Json(new { firstList = Name, secondList = Value }, JsonRequestBehavior.AllowGet);
        }
       






    }
}



public static class StationeryExtension
{
    public static double? UnitPrice(this Stationery s, int supplierId)
    {
        foreach (StationerySupplier ss in s.StationerySuppliers)
        {
            if (ss.SupplierId == supplierId) return ss.Price;
        }
        return null;
    }

    public static double LinePrice(this Stationery s, int supplierId, int? qty)
    {
        foreach (StationerySupplier ss in s.StationerySuppliers)
        {
            if (ss.SupplierId == supplierId) return Convert.ToDouble(ss.Price * qty);
        }
        return 0;
    }

    public static Supplier PrimarySupplier(this Stationery s)
    {
        foreach (StationerySupplier ss in s.StationerySuppliers)
        {
            if (ss.Rank == 1) return ss.Supplier;
        }
        return null;
    }

}

