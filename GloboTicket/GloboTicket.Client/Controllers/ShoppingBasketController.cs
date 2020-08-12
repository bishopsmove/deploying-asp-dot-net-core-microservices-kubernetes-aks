﻿using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models;
using GloboTicket.Web.Models.Api;
using GloboTicket.Web.Models.View;
using GloboTicket.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GloboTicket.Web.Controllers
{
    public class ShoppingBasketController : Controller
    {
        private readonly IShoppingBasketService basketService;
        private readonly IDiscountService discountService;
        private readonly Settings settings;

        public ShoppingBasketController(IShoppingBasketService basketService, Settings settings, IDiscountService discountService)
        {
            this.basketService = basketService;
            this.settings = settings;
            this.discountService = discountService;
        }

        public async Task<IActionResult> Index()
        {
            var basketViewModel = await CreateBasketViewModel();

            return View(basketViewModel);
        }

        private async Task<BasketViewModel> CreateBasketViewModel()
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            var basket = await basketService.GetBasket(basketId);

            var basketLines = await basketService.GetLinesForBasket(basketId);

            var lineViewModels = basketLines.Select(bl => new BasketLineViewModel
                {
                    LineId = bl.BasketLineId,
                    EventId = bl.EventId,
                    EventName = bl.Event.Name,
                    Date = bl.Event.Date,
                    Price = bl.Price,
                    Quantity = bl.TicketAmount
                }
            );

            var basketViewModel = new BasketViewModel
            {
                BasketLines = lineViewModels.ToList(),
                Code = basket.Code,
                Discount = basket.Discount
            };

            basketViewModel.ShoppingCartTotal = basketViewModel.BasketLines.Sum(bl => bl.Price * bl.Quantity);
            
            return basketViewModel;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLine(BasketLineForCreation basketLine)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            var newLine = await basketService.AddToBasket(basketId, basketLine);
            Response.Cookies.Append(settings.BasketIdCookieName, newLine.BasketId.ToString());

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLine(BasketLineForUpdate basketLineUpdate)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            await basketService.UpdateLine(basketId, basketLineUpdate);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> RemoveLine(Guid lineId)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            await basketService.RemoveLine(basketId, lineId);
            return RedirectToAction("Index");
        }

        public IActionResult Checkout()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Checkout(BasketCheckoutViewModel basketCheckoutViewModel)
        {
            return RedirectToAction("CheckoutComplete");
        }

        [HttpPost]
        public async Task<IActionResult> ApplyDiscountCode(string code)//TODO: refactor to use just single value
        {

            var coupon =await discountService.GetCouponByCode(code);

            if (coupon == null || coupon.AlreadyUsed) return RedirectToAction("Index");

            //coupon will be applied to the basket
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            await basketService.ApplyCouponToBasket(basketId, new CouponForUpdate() {CouponId = coupon.CouponId});
            await discountService.UseCoupon(coupon.CouponId);

            return RedirectToAction("Index");

        }

        public IActionResult CheckoutComplete()
        {
            return View();
        }
    }
}
