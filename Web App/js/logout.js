// logout.js
document.addEventListener("DOMContentLoaded", () => {
  const logoutBtn = document.getElementById("logoutBtn");
  
  if (logoutBtn) {
    logoutBtn.addEventListener("click", (e) => {
      e.preventDefault(); // prevent default link behavior

      // Remove the current user from sessionStorage
      sessionStorage.removeItem("currentUser");

      // Redirect to login page
      window.location.href = "/login.html";
    });
  }
});
