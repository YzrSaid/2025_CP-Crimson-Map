

document.addEventListener("DOMContentLoaded", () => {
  const userNameSpan = document.getElementById("userName");

  
  const currentUser = JSON.parse(sessionStorage.getItem("currentUser"));

  if (currentUser && currentUser.firstName) {
    
    const cleanedName = currentUser.firstName.trim().toLowerCase();

    
    const formattedName = cleanedName
      .split(" ")
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

    userNameSpan.textContent = `${formattedName}!`;
  } else {
    
    window.location.href = "/html/login.html";
  }
});
