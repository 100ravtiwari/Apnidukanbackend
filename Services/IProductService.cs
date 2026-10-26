using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Products;

namespace Dukan.API.Services;

public interface IProductService
{
    Task<ApiResponse<PagedResult<ProductDto>>> GetProductsAsync(ProductQueryParams query);
    Task<ApiResponse<ProductDto>> GetProductByIdAsync(int id);
    Task<ApiResponse<ProductDto>> CreateProductAsync(CreateProductDto dto);
    Task<ApiResponse<ProductDto>> UpdateProductAsync(int id, UpdateProductDto dto);
    Task<ApiResponse<bool>> DeleteProductAsync(int id);
    Task<ApiResponse<ProductDto>> UpdateStockAsync(int id, int newStock);
    Task<ApiResponse<ProductDto>> UpdatePriceAsync(int id, decimal price, decimal? discountPrice);
}
