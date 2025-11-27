using Corsinvest.Fx.Functional;

namespace Corsinvest.Fx.Examples;

/// <summary>
/// Example 03: ResultOf Railway-Oriented Programming
///
/// Demonstrates a complete order processing pipeline:
/// - Multi-step business logic with error handling
/// - Railway-Oriented Programming pattern
/// - Error propagation without exceptions
/// - Side effects with Tap
/// </summary>
public static class ResultOfRailway
{
    public static void Run()
    {
        Console.WriteLine("\n═══ Example 03: Railway-Oriented Programming ═══\n");

        // Test scenarios
        var orders = new[]
        {
            new OrderRequest(CustomerId: 1, ProductId: 101, Quantity: 2),
            new OrderRequest(CustomerId: 999, ProductId: 101, Quantity: 2),  // Customer not found
            new OrderRequest(CustomerId: 1, ProductId: 999, Quantity: 2),    // Product not found
            new OrderRequest(CustomerId: 1, ProductId: 101, Quantity: 1000), // Insufficient stock
            new OrderRequest(CustomerId: 2, ProductId: 102, Quantity: 1)     // Payment failed
        };

        foreach (var orderRequest in orders)
        {
            Console.WriteLine($"📦 Processing Order: Customer={orderRequest.CustomerId}, Product={orderRequest.ProductId}, Qty={orderRequest.Quantity}");

            var result = ProcessOrder(orderRequest);

            result.Match(
                ok => Console.WriteLine($"   ✅ Order #{ok.Value.OrderId} completed! Total: ${ok.Value.TotalAmount:F2}"),
                fail => Console.WriteLine($"   ❌ Order failed: {fail.ErrorValue}")
            );

            Console.WriteLine();
        }
    }

    // Main railway pipeline
    private static ResultOf<Order, OrderError> ProcessOrder(OrderRequest request)
    {
        // Each step returns ResultOf - if any fails, the entire pipeline short-circuits
        return ValidateOrder(request)
            .TapOk(_ => Console.WriteLine("   → Order validated"))
            .Bind(GetCustomer)
            .TapOk(customer => Console.WriteLine($"   → Customer found: {customer.Name}"))
            .Bind(customer => GetProduct(request.ProductId).Map(product => (customer, product)))
            .TapOk(tuple => Console.WriteLine($"   → Product found: {tuple.product.Name}"))
            .Bind(tuple => CheckInventory(tuple.product, request.Quantity).Map(_ => tuple))
            .TapOk(_ => Console.WriteLine("   → Inventory check passed"))
            .Bind(tuple => CalculateTotal(tuple.product, request.Quantity).Map(total => (tuple.customer, tuple.product, total)))
            .TapOk(data => Console.WriteLine($"   → Total calculated: ${data.total:F2}"))
            .Bind(data => ProcessPayment(data.customer, data.total).Map(_ => data))
            .TapOk(_ => Console.WriteLine("   → Payment processed"))
            .Map(data => new Order(
                OrderId: Random.Shared.Next(10000, 99999),
                CustomerId: data.customer.Id,
                ProductId: data.product.Id,
                Quantity: request.Quantity,
                TotalAmount: data.total
            ))
            .TapOk(order => Console.WriteLine($"   → Order created: #{order.OrderId}"));
    }

    // Step 1: Validate order request
    private static ResultOf<OrderRequest, OrderError> ValidateOrder(OrderRequest request)
    {
        if (request.Quantity <= 0)
        {
            return ResultOf.Fail<OrderRequest, OrderError>(OrderError.InvalidQuantity);
        }

        return ResultOf.Ok<OrderRequest, OrderError>(request);
    }

    // Step 2: Get customer
    private static ResultOf<Customer, OrderError> GetCustomer(OrderRequest request)
    {
        var customers = new Dictionary<int, Customer>
        {
            [1] = new Customer(1, "Alice", "alice@example.com"),
            [2] = new Customer(2, "Bob", "bob@example.com")
        };

        return customers.TryGetValue(request.CustomerId, out var customer)
            ? ResultOf.Ok<Customer, OrderError>(customer)
            : ResultOf.Fail<Customer, OrderError>(OrderError.CustomerNotFound);
    }

    // Step 3: Get product
    private static ResultOf<Product, OrderError> GetProduct(int productId)
    {
        var products = new Dictionary<int, Product>
        {
            [101] = new Product(101, "Laptop", 999.99m, 10),
            [102] = new Product(102, "Mouse", 29.99m, 50)
        };

        return products.TryGetValue(productId, out var product)
            ? ResultOf.Ok<Product, OrderError>(product)
            : ResultOf.Fail<Product, OrderError>(OrderError.ProductNotFound);
    }

    // Step 4: Check inventory
    private static ResultOf<Unit, OrderError> CheckInventory(Product product, int quantity)
    {
        if (product.Stock < quantity)
        {
            return ResultOf.Fail<Unit, OrderError>(OrderError.InsufficientStock);
        }

        return ResultOf.Ok<Unit, OrderError>(Unit.Value);
    }

    // Step 5: Calculate total
    private static ResultOf<decimal, OrderError> CalculateTotal(Product product, int quantity)
    {
        var total = product.Price * quantity;
        return ResultOf.Ok<decimal, OrderError>(total);
    }

    // Step 6: Process payment
    private static ResultOf<Unit, OrderError> ProcessPayment(Customer customer, decimal amount)
    {
        // Simulate payment failure for Bob
        if (customer.Id == 2)
        {
            return ResultOf.Fail<Unit, OrderError>(OrderError.PaymentFailed);
        }

        return ResultOf.Ok<Unit, OrderError>(Unit.Value);
    }

    // Domain models
    private record OrderRequest(int CustomerId, int ProductId, int Quantity);
    private record Customer(int Id, string Name, string Email);
    private record Product(int Id, string Name, decimal Price, int Stock);
    private record Order(int OrderId, int CustomerId, int ProductId, int Quantity, decimal TotalAmount);

    // Error types
    private enum OrderError
    {
        InvalidQuantity,
        CustomerNotFound,
        ProductNotFound,
        InsufficientStock,
        PaymentFailed
    }
}
