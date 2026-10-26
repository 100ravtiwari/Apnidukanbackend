using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Products;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetProducts([FromQuery] ProductQueryParams query)
    {
        var response = await _productService.GetProductsAsync(query);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetProductById(int id)
    {
        var response = await _productService.GetProductByIdAsync(id);
        if (!response.Success)
        {
            return NotFound(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct([FromBody] CreateProductDto dto)
    {
        var response = await _productService.CreateProductAsync(dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return CreatedAtAction(nameof(GetProductById), new { id = response.Data?.Id }, response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
    {
        var response = await _productService.UpdateProductAsync(id, dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteProduct(int id)
    {
        var response = await _productService.DeleteProductAsync(id);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}/stock")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateStock(int id, [FromBody] UpdateStockDto dto)
    {
        var response = await _productService.UpdateStockAsync(id, dto.StockQuantity);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}/price")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> UpdatePrice(int id, [FromBody] UpdatePriceDto dto)
    {
        var response = await _productService.UpdatePriceAsync(id, dto.Price, dto.DiscountPrice);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
