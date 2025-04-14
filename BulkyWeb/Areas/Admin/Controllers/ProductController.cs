using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties:"Category").ToList();

            return View(objProductList);
        }

        public IActionResult Upsert(int? id)
        {
            IEnumerable<SelectListItem> CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });

            ProductVM productVM = new ProductVM()
            {
                CategoryList = CategoryList,
                Product = new Product()
            };

            if (id == null || id == 0)
            {
                return View(productVM);
            }
            else
            {
                productVM.Product = _unitOfWork.Product.Get(u => u.Id == id, includeProperties: "ProductImages");
                
                return View(productVM);
            }

        }

        [HttpPost]
        public IActionResult Upsert(ProductVM obj, List<IFormFile>? files)
        {
            if (ModelState.IsValid)
            {
                if (obj.Product.Id == 0)
                {
                    _unitOfWork.Product.Add(obj.Product);
                }
                else
                {
                    _unitOfWork.Product.Update(obj.Product);
                }

                _unitOfWork.Save();

                string wwwRootPath = _webHostEnvironment.WebRootPath; 
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var productPath = @"images\products\product-" + obj.Product.Id;
                        var path = Path.Combine(wwwRootPath, productPath);

                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        using (var fileStream = new FileStream(Path.Combine(path, fileName), FileMode.Create))
                        {
                            file.CopyTo(fileStream);
                        }

                        ProductImage productImage = new ProductImage()
                        {
                            ProductId = obj.Product.Id,
                            ImageUrl = @"\" + productPath + @"\" + fileName
                        };

                        if (obj.Product.ProductImages == null)
                        {
                            obj.Product.ProductImages = new List<ProductImage>();
                        }

                        obj.Product.ProductImages.Add(productImage);

                    }

                    _unitOfWork.Product.Update(obj.Product);
                    _unitOfWork.Save();
                }

                
                TempData["success"] = "Product created / updated successfully";

                return RedirectToAction("Index");
            }
            else
            {
                obj.CategoryList = _unitOfWork.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                return View(obj);
            }
        }

        public IActionResult DeleteImage(int imageId)
        {
            var imageToDelete = _unitOfWork.ProductImage.Get(u => u.Id == imageId);
            if (imageToDelete != null)
            {
                if (!string.IsNullOrEmpty(imageToDelete.ImageUrl))
                {
                    var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageToDelete.ImageUrl.TrimStart('\\'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                _unitOfWork.ProductImage.Remove(imageToDelete);
                _unitOfWork.Save();

                TempData["success"] = "Image deleted successfully";
            }

            return RedirectToAction(nameof(Upsert), new { id = imageToDelete.ProductId });
        }

        #region API CALLS

        [HttpGet]
        public IActionResult GetAll(int id)
        {
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();

            return Json(new { data = objProductList });
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var obj = _unitOfWork.Product.Get(u => u.Id == id);
            if (obj == null)
            {
                return Json(new { success = false, message = "Error while deleting." });
            }

            var productPath = @"images\products\product-" + id;
            var path = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

            if (Directory.Exists(path))
            {
                string[] filePaths = Directory.GetFiles(path);
                foreach (string filePath in filePaths)
                {
                    System.IO.File.Delete(filePath);
                }

                Directory.Delete(path);
            }

            _unitOfWork.Product.Remove(obj);
            _unitOfWork.Save();

            return Json(new { success = true, message = "Delete Successful." });
        }

        #endregion
    }
}
