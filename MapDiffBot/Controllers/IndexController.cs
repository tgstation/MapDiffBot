using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapDiffBot.Controllers {
    [Route("")] // Present a blank code 200 page so uptime monitors can use the service
    public class IndexController : Controller {
        public IActionResult Index() {
            return Ok("Welcome to MapDiffBot");
        }
    }
}
