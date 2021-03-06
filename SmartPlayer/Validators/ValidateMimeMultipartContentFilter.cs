﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.IO;

namespace SmartPlayer.Validators
{
    public class ValidateMimeMultipartContentFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            if (!actionContext.Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }
        }
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {

       }
    }
}