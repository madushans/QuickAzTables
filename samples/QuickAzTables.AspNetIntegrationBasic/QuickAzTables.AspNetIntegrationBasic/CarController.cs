using Microsoft.AspNetCore.Mvc;

namespace QuickAzTables.AspNetIntegrationBasic
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarController : ControllerBase
    {
        private readonly TypedTableStore<Car> _store;

        public CarController(TypedTableStore<Car> store)
        {
            _store = store;
        }

        /// <summary>
        /// can be invoked by GET /api/Car?plateNumber=whatup&city=gotham
        /// This will return a 404 since there are no rows.
        /// </summary>
        /// <param name="city"></param>
        /// <param name="plateNumber"></param>
        /// <returns></returns>
        [HttpGet] 
        public async Task<ActionResult<Car>> Get(string city, string plateNumber)
        {
            var car = await _store.QuerySingleAsync(city, plateNumber);
            if (car is null) return NotFound();

            return Ok(car);
        }
    }
}
