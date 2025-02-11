﻿using faka.Models;
using faka.Models.Dtos;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace faka.Payment.Gateways;

public class StripeAlipayPaymentGateway : IPaymentGateway
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StripeAlipayPaymentGateway(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
        StripeConfiguration.ApiKey = configuration["PaymentGateways:Stripe:ApiKey"];
    }

    private static string ConfigSection => "Stripe";
    public string Name => "Stripe";

    public async Task<GatewayResponse> CreateAsync(Order order, OrderPayDto orderPayDto)
    {
        // 使用 Stripe API 创建支付
        //创建 checkout session
        if (order.Product == null) throw new ArgumentException("Product is null");
        var product = order.Product;

        var customerService = new CustomerService();
        var stripeCustomers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = order.Email
        });

        var stripeCustomer = stripeCustomers.FirstOrDefault();
        if (stripeCustomer == null)
        {
            var user = order.User;
            CustomerCreateOptions newCustomer;
            if (user == null)
                newCustomer = new CustomerCreateOptions
                {
                    Email = order.Email,
                    Name = "Guest"
                };
            else
                newCustomer = new CustomerCreateOptions
                {
                    Email = order.Email,
                    Name = user.UserName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "UserId", user.Id }
                    }
                };
            stripeCustomer = await customerService.CreateAsync(newCustomer);
        }

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string>
            {
                "alipay",
                "wechat_pay",
                "card"
            },
            PaymentMethodOptions = new SessionPaymentMethodOptionsOptions
            {
                WechatPay = new SessionPaymentMethodOptionsWechatPayOptions
                {
                    Client = "web"
                }
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = Convert.ToInt64(order.Amount * 100),
                        Currency = "cny",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = product.Name,
                            Description = product.Description
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            Customer = stripeCustomer.Id,
            SuccessUrl = orderPayDto.ReturnUrl,
            CancelUrl = orderPayDto.ReturnUrl
        };
        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return new GatewayResponse
        {
            Status = GatewayStatus.Success,
            Gateway = Name,
            TradeNumber = session.Id,
            PaymentUrl = session.Url
        };
    }
}