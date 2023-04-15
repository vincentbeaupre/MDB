using Antlr.Runtime.Misc;
using MDB.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Web.Mvc;

namespace MDB.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDBEntities DB = new AppDBEntities();

        [HttpPost]
        public JsonResult EmailExist(string email)
        {
            return Json(DB.EmailExist(email));
        }

        [HttpPost]
        public JsonResult EmailAvailable(string Email, int Id)
        {
            return Json(DB.EmailAvailable(Email, Id));
        }

        #region Login and Logout
        public ActionResult Login(string message)
        {
            ViewBag.Message = message;
            return View(new LoginCredential());
        }
        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult Login(LoginCredential loginCredential)
        {
            if (ModelState.IsValid)
            {
                if (DB.EmailBlocked(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce compte est bloqué.");
                    return View(loginCredential);
                }
                if (!DB.EmailVerified(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce courriel n'est pas vérifié.");
                    return View(loginCredential);
                }
                User user = DB.GetUser(loginCredential);
                if (user == null)
                {
                    ModelState.AddModelError("Password", "Mot de passe incorrecte.");
                    return View(loginCredential);
                }
                if (OnlineUsers.IsOnLine(user.Id))
                {
                    ModelState.AddModelError("Email", "Cet usager est déjà connecté.");
                    return View(loginCredential);
                }
                OnlineUsers.AddSessionUser(user.Id);
                Session["currentLoginId"] = DB.AddLogin(user.Id).Id;
                return RedirectToAction("Index", "Movies");
            }
            return View(loginCredential);
        }
        public ActionResult Logout()
        {
            if (Session["currentLoginId"] != null)
                DB.UpdateLogout((int)Session["currentLoginId"]);
            OnlineUsers.RemoveSessionUser();
            return RedirectToAction("Login");
        }
        #endregion

        public ActionResult Subscribe()
        {
            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            return View(new User());
        }

        [HttpPost]
        public ActionResult Subscribe(User user)
        {
            if (ModelState.IsValid)
            {
                if (DB.AddUser(user) != null)
                {
                    SendEmailVerification(user, user.Email);
                }

                return RedirectToAction("SubscribeDone", new { id = user.Id });
            }
            return View(user);
        }
        public ActionResult Profil()
        {
            User user = OnlineUsers.GetSessionUser();
            if (user != null)
            {
                ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
                Session["CurrentEmail"] = user.Email;
                Session["CurrentPassword"] = user.Password;
                Session["UnchangedPasswordCode"] = Guid.NewGuid().ToString();
                return View(user);
            }
            return RedirectToAction("Login");
        }
        [HttpPost]
        public ActionResult Profil(User user)
        {
            bool emailHasChanged = false;

            if (ModelState.IsValid)
            {
                string newEmail = user.Email;

                if ((string)Session["UnchangedPasswordCode"] == user.Password)
                {
                    user.ConfirmPassword = user.Password = (string)Session["CurrentPassword"];
                }

                if ((string)Session["CurrentEmail"] != user.Email)
                {
                    user.ConfirmEmail = user.Email = (string)Session["CurrentEmail"];
                    emailHasChanged = true;
                }

                if (DB.UpdateUser(user) != null)
                {
                    if (emailHasChanged)
                    {
                        SendEmailVerification(user, newEmail);
                        return RedirectToAction("EmailChangedAlert", new { id = user.Id });
                    }
                }

                return RedirectToAction("Index", "Movies");
            }
            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            Session["CurrentEmail"] = user.Email;
            Session["CurrentPassword"] = user.Password;
            Session["UnchangedPasswordCode"] = Guid.NewGuid().ToString();
            return View(user);
        }
        public ActionResult SubscribeDone(int id = 0)
        {
            if (id != 0)
            {
                User user = DB.Users.Find(id);
                if (user == null)
                {
                    return HttpNotFound();
                }
                return View(user);
            }
            else
            {
                return HttpNotFound();
            }
        }
        public ActionResult ResetPasswordCommand()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ResetPasswordCommand(EmailView model)
        {
            if (ModelState.IsValid)
            {
                string email = model.Email;

                if (DB.EmailExist(email))
                {
                    SendPasswordReset(email);
                    return RedirectToAction("ResetPasswordCommandAlert"); 
                }
                else
                {
                    return View(model);
                }
            }
            else
            {
                return View(model);
            }
        }
        public ActionResult ResetPasswordCommandAlert()
        {
            return View();
        }
        public ActionResult ResetPassword(int userId, int code)
        {
            ResetPasswordCommand resetPasswordCommand = DB.FindResetPasswordCommand(userId, code);
            
            if(resetPasswordCommand != null)
            {
                var model = new PasswordView { UserId = userId };
                return View(model);
            }
            else
            {
                return RedirectToAction("ResetPasswordError");
            }
        }
        [HttpPost]
        public ActionResult ResetPassword(PasswordView model)
        {
            if (ModelState.IsValid)
            {
                if(DB.ResetPassword(model.UserId, model.Password))
                {
                    return RedirectToAction("ResetPasswordSuccess");
                }
                else
                {
                    return RedirectToAction("ResetPasswordError");
                }
            }
            return View(model);
        }
        public ActionResult ResetPasswordSuccess()
        {
            return View();
        }
        public ActionResult ResetPasswordError()
        {
            return View();
        }
        public ActionResult EmailChangedAlert(int id = 0)
        {
            if (id != 0)
            {
                User user = DB.Users.Find(id);
                if (user == null)
                {
                    return HttpNotFound();
                }
                return View(user);
            }
            else
            {
                return HttpNotFound();
            }

        }
        public ActionResult VerifyUser(int userId, int code)
        {
            bool verified = DB.VerifyUser(userId, code);

            if (verified)
            {
                return RedirectToAction("VerifyDone", new { id = userId });
            }
            else
            {
                return RedirectToAction("VerifyError");
            }
        }
        public ActionResult VerifyDone(int id)
        {
            User user = DB.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            return View(user);
        }
        public ActionResult VerifyError()
        {
            return View();
        }
        public void SendEmailVerification(User user, string newEmail)
        {
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.Add_UnverifiedEmail(user.Id, newEmail);
                if (unverifiedEmail != null)
                {
                    string verificationUrl = Url.Action("VerifyUser", "Accounts", null, Request.Url.Scheme);
                    String Link = @"<br/><a href='" + verificationUrl + "?userid=" + user.Id + "&code=" + unverifiedEmail.VerificationCode + @"' > Confirmez votre inscription...</a>";

                    String suffixe = "";
                    if (user.GenderId == 2)
                    {
                        suffixe = "e";
                    }
                    string Subject = "MDB - Vérification d'inscription...";

                    string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                    Body += @"Merci de vous être inscrit" + suffixe + " au site MDB. <br/>";
                    Body += @"Pour utiliser votre compte vous devez confirmer votre inscription en cliquant sur le lien suivant : <br/>";
                    Body += Link;
                    Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                    Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='mailto:"
                         + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site MDB)";

                    SMTP.SendEmail(user.GetFullName(), unverifiedEmail.Email, Subject, Body);
                }
            }
        }
        public void SendPasswordReset(string email)
        {

            ResetPasswordCommand resetPasswordCommand= DB.AddResetPasswordCommand(email);
            if (resetPasswordCommand != null)
            {
                User user = DB.FindUser(resetPasswordCommand.UserId);

                string resetUrl = Url.Action("ResetPassword", "Accounts", null, Request.Url.Scheme);
                String Link = @"<br/><a href='" + resetUrl + "?userid=" + resetPasswordCommand.UserId + "&code=" + resetPasswordCommand.VerificationCode + @"' > Modifiez votre mot de passe...</a>";

                string Subject = "MDB - Réinitialisation de mot de passe...";

                string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                Body += @"Vous avez demandé de réinitialiser votre mot de passe.  <br/>";
                Body += @"Procédez en cliquant sur le lien suivant : <br/>";
                Body += Link;
                Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='mailto:"
                     + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site MDB)";

                SMTP.SendEmail(user.GetFullName(), user.Email, Subject, Body);
            }

        }
    }
}