using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>
/// 商品目录只读契约：表现层与驱动层查询商品/分类所需的全部能力。
///
/// 设计说明：<see cref="PosRepository"/> 是数据访问的唯一落点，但它同时承担了
/// 商品、设备、订单三类职责。为了让调用方只依赖"自己真正需要的那部分能力"，
/// 这里按最小契约切出一个只读视图，由 <see cref="PosRepository"/> 直接实现
/// （不拆分类型、不改动仓储既有成员，保证单实例与既有绑定继续有效）。
/// </summary>
/// <remarks>
/// <b>批次 3 起不再暴露可变商品列表</b>：原 <c>List&lt;Product&gt; Products</c> 被
/// <see cref="ProductCount"/> + <see cref="GetProductAt"/> 取代。
/// 原因是收银台曾把该列表直接交给 <c>ListCollectionView</c> 做全量过滤，
/// 使表现层被"必须是一个具体可变 <c>List</c>"这一实现细节绑架；
/// 列表浏览已迁移到 <see cref="IProductQuery"/>，这里只保留
/// "总数 + 按序号取样"这两个不可变查询能力，供模拟扫码枪等场景使用。
/// </remarks>
public interface IProductCatalog
{
    /// <summary>带"全部"虚拟分类的分类列表，供收银台分类标签使用。</summary>
    IReadOnlyList<ProductCategory> CategoriesWithAll { get; }

    /// <summary>"全部"虚拟分类的 Id。</summary>
    string AllCategoryId { get; }

    /// <summary>当前商品总数。</summary>
    int ProductCount { get; }

    /// <summary>
    /// 按序号取商品（越界返回 <c>null</c>）。
    /// </summary>
    /// <remarks>面向"按索引取样"的调用方（如模拟扫码枪随机挑一件商品），避免为取一个元素而暴露整个列表。</remarks>
    Product? GetProductAt(int index);

    /// <summary>
    /// 取某分类下的商品（分类索引切片；<paramref name="categoryId"/> 等于"全部"时返回全量商品）。
    /// </summary>
    /// <remarks>返回值是只读视图，调用方不得修改；由 <see cref="IProductQuery"/> 消费。</remarks>
    IReadOnlyList<Product> GetProductsInCategory(string categoryId);

    /// <summary>按条码查找商品（扫码枪场景）。</summary>
    Product? FindByBarcode(string barcode);

    /// <summary>按商品编码查找。</summary>
    Product? FindByCode(string code);
}
