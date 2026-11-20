using eVrticSistem.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class PasswordConfirmationModelTests
{
    [TestMethod]
    public void ForgotPasswordConfirmationGetOstavljaModelIspravnim()
    {
        var stranica = new ForgotPasswordConfirmationModel
        {
            PageContext = new PageContext()
        };

        stranica.OnGet();

        Assert.IsTrue(stranica.ModelState.IsValid);
    }

    [TestMethod]
    public void ResetPasswordConfirmationGetOstavljaModelIspravnim()
    {
        var stranica = new ResetPasswordConfirmationModel
        {
            PageContext = new PageContext()
        };

        stranica.OnGet();

        Assert.IsTrue(stranica.ModelState.IsValid);
    }

    [TestMethod]
public void ForgotPasswordConfirmationDozvoljavaAnonimanPristup()
{
    Assert.IsTrue(typeof(ForgotPasswordConfirmationModel)
        .IsDefined(typeof(AllowAnonymousAttribute), inherit: true));
}

[TestMethod]
public void ResetPasswordConfirmationDozvoljavaAnonimanPristup()
{
    Assert.IsTrue(typeof(ResetPasswordConfirmationModel)
        .IsDefined(typeof(AllowAnonymousAttribute), inherit: true));
}
}