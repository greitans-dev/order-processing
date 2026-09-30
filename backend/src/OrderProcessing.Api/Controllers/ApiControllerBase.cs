using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase;
