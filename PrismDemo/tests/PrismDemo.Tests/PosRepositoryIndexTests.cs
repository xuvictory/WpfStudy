using PrismDemo.Models;
using PrismDemo.Services;
using Xunit;

namespace PrismDemo.Tests;

/// <summary>
/// <see cref="PosRepository"/> 索引化改造的一致性回归。
///
/// 索引化的风险不在"快不快"，而在"准不准"：一旦索引与 <see cref="PosRepository.Products"/>
/// 脱节，扫码就会找不到商品。因此这里覆盖三类场景：
/// 1) 派生索引在数据变化后必须重建（可变集合被直接 Add、ReplaceAll 条数不变）；
/// 2) 匹配语义必须与旧线性扫描完全一致（大小写、去空格、重复项、空值跳过）；
/// 3) <see cref="PosRepository.CategoriesWithAll"/> 缓存必须随分类变化失效。
/// </summary>
public sealed class PosRepositoryIndexTests
{
    private static Product Product(string code, string barcode, string categoryId = "drink", string name = "") => new()
    {
        Code = code,
        Name = string.IsNullOrEmpty(name) ? $"商品{code}" : name,
        Barcode = barcode,
        CategoryId = categoryId,
        Price = 1m,
        Stock = 10
    };

    private static PosRepository BuildRepository(params Product[] products)
    {
        var repository = new PosRepository();
        repository.ReplaceAll(
            new[] { new ProductCategory { Id = "drink", Name = "饮料", Short = "饮" } },
            products,
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());
        return repository;
    }

    [Fact]
    public void FindByBarcode_LookupAfterDirectAdd_SeesNewProduct()
    {
        // 关键场景：索引已建立后，调用方（如测试/其它服务）直接往 Products 里加商品。
        var repository = BuildRepository(Product("A1", "6900000000001"));
        Assert.NotNull(repository.FindByBarcode("6900000000001"));

        repository.Products.Add(Product("B2", "6900000000002"));

        Assert.Same(repository.Products[1], repository.FindByBarcode("6900000000002"));
        Assert.Same(repository.Products[1], repository.FindByCode("B2"));
    }

    [Fact]
    public void ReplaceAll_SameCountDifferentContent_InvalidatesIndex()
    {
        // 条数相同、内容不同：仅靠条数检测无法发现，必须依赖 ReplaceAll 显式失效。
        var repository = BuildRepository(Product("OLD", "6900000000001"));
        Assert.NotNull(repository.FindByBarcode("6900000000001"));

        repository.ReplaceAll(
            new[] { new ProductCategory { Id = "drink", Name = "饮料", Short = "饮" } },
            new[] { Product("NEW", "6900000000002") },
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());

        Assert.Null(repository.FindByBarcode("6900000000001"));
        Assert.Equal("NEW", repository.FindByBarcode("6900000000002")!.Code);
        Assert.Equal("NEW", repository.FindByCode("NEW")!.Code);
        Assert.Null(repository.FindByCode("OLD"));
    }

    [Fact]
    public void FindByBarcode_TrimsInputAndIgnoresBlankBarcodes()
    {
        var blank = Product("BLANK", "   ");
        var real = Product("REAL", "6900000000003");
        var repository = BuildRepository(blank, real);

        Assert.Same(real, repository.FindByBarcode("  6900000000003  "));
        Assert.Null(repository.FindByBarcode("   "));
    }

    [Fact]
    public void FindByCode_IsCaseInsensitiveAndCodeEmptyStillMatches()
    {
        var repository = BuildRepository(Product("abc-01", "6900000000004", name: "混合大小写"));

        Assert.Equal("abc-01", repository.FindByCode("ABC-01")!.Code);
    }

    [Fact]
    public void FindByBarcode_DuplicateBarcode_ReturnsFirstRegistered()
    {
        var first = Product("FIRST", "6900000000005");
        var second = Product("SECOND", "6900000000005");
        var repository = BuildRepository(first, second);

        Assert.Same(first, repository.FindByBarcode("6900000000005"));
    }

    [Fact]
    public void FindByCode_DuplicateCodeIgnoringCase_ReturnsFirstRegistered()
    {
        var first = Product("DUP", "6900000000006");
        var second = Product("dup", "6900000000007");
        var repository = BuildRepository(first, second);

        Assert.Same(first, repository.FindByCode("dup"));
    }

    [Fact]
    public void FindByCode_NullInput_MatchesProductWithNullCode()
    {
        var withCode = Product("C1", "6900000000008");
        var withoutCode = new Product { Code = null!, Name = "无编码", Barcode = "6900000000009" };
        var repository = BuildRepository(withCode, withoutCode);

        Assert.Same(withoutCode, repository.FindByCode(null!));
        Assert.Null(repository.FindByCode("NOPE"));
    }

    [Fact]
    public void CategoriesWithAll_IsCachedUntilCategoriesChange()
    {
        var repository = BuildRepository(Product("A1", "6900000000010"));

        var first = repository.CategoriesWithAll;
        var second = repository.CategoriesWithAll;

        Assert.Same(first, second);
        Assert.Equal(2, first.Count);
        Assert.Equal(repository.AllCategoryId, first[0].Id);

        repository.ReplaceAll(
            new[]
            {
                new ProductCategory { Id = "drink", Name = "饮料", Short = "饮" },
                new ProductCategory { Id = "food", Name = "食品", Short = "食" }
            },
            Array.Empty<Product>(),
            Array.Empty<Order>(),
            Array.Empty<DeviceInfo>());

        var rebuilt = repository.CategoriesWithAll;
        Assert.NotSame(first, rebuilt);
        Assert.Equal(3, rebuilt.Count);
    }

    [Fact]
    public void CategoriesWithAll_ReflectsDirectCategoryAdd()
    {
        var repository = BuildRepository(Product("A1", "6900000000011"));
        var before = repository.CategoriesWithAll;

        repository.Categories.Add(new ProductCategory { Id = "food", Name = "食品", Short = "食" });

        var after = repository.CategoriesWithAll;
        Assert.NotSame(before, after);
        Assert.Equal(3, after.Count);
    }

    [Fact]
    public void GetProductsInCategory_ReturnsOnlyMatchingInOriginalOrder()
    {
        var d1 = Product("D1", "6900000000020", "drink");
        var f1 = Product("F1", "6900000000021", "food");
        var d2 = Product("D2", "6900000000022", "drink");
        var repository = BuildRepository(d1, f1, d2);

        var drinks = repository.GetProductsInCategory("drink");

        Assert.Equal(2, drinks.Count);
        Assert.Same(d1, drinks[0]);
        Assert.Same(d2, drinks[1]);
        Assert.Equal(new[] { "D1", "D2" }, repository.GetProductsInCategory("drink").Select(p => p.Code));
    }

    [Fact]
    public void GetProductsInCategory_IsCaseInsensitiveAndUnknownReturnsEmpty()
    {
        var repository = BuildRepository(Product("D1", "6900000000023", "drink"));

        Assert.Single(repository.GetProductsInCategory("DRINK"));
        Assert.Empty(repository.GetProductsInCategory("no-such-category"));
    }

    [Fact]
    public void GetProductsInCategory_AllCategory_ReturnsFullProductList()
    {
        var repository = BuildRepository(
            Product("D1", "6900000000024", "drink"),
            Product("F1", "6900000000025", "food"));

        var all = repository.GetProductsInCategory(repository.AllCategoryId);

        Assert.Same(repository.Products, all);
    }

    [Fact]
    public void GetProductsInCategory_ReflectsProductsAddedAfterFirstSlice()
    {
        var repository = BuildRepository(Product("D1", "6900000000026", "drink"));
        Assert.Single(repository.GetProductsInCategory("drink"));

        repository.Products.Add(Product("D2", "6900000000027", "drink"));

        Assert.Equal(2, repository.GetProductsInCategory("drink").Count);
    }
}
