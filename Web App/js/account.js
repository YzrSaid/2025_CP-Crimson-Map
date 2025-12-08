


import { firebaseConfig } from "../firebaseConfig.mjs";
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-app.js";
import { getFirestore, collection, addDoc, serverTimestamp } from "https://www.gstatic.com/firebasejs/10.12.0/firebase-firestore.js";
import CryptoJS from "https://cdn.jsdelivr.net/npm/crypto-js@4.2.0/+esm";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);




const addUserBtn = document.querySelector(".add-user-btn");
const addUserModal = document.getElementById("addUserModal");
const closeBtn = addUserModal?.querySelector(".close-btn");
const cancelBtn = addUserModal?.querySelector(".cancel-btn");
const addUserForm = document.getElementById("addUserForm");




addUserBtn?.addEventListener("click", () => {
  addUserModal.style.display = "flex";
});

closeBtn?.addEventListener("click", () => {
  addUserModal.style.display = "none";
});

cancelBtn?.addEventListener("click", () => {
  addUserModal.style.display = "none";
});

window.addEventListener("click", (e) => {
  if (e.target === addUserModal) addUserModal.style.display = "none";
});




addUserForm?.addEventListener("submit", async (e) => {
  e.preventDefault();

  const firstName = document.getElementById("firstName").value.trim();
  const middleInitial = document.getElementById("middleInitial").value.trim();
  const lastName = document.getElementById("lastName").value.trim();
  const contactNumber = document.getElementById("contactNumber").value.trim();
  const email = document.getElementById("modalEmail").value.trim(); 
  const password = document.getElementById("modalPassword").value; 

  if (!firstName || !lastName || !contactNumber || !email || !password) {
    alert("Please fill out all required fields.");
    return;
  }

  try {
    const secretKey = "CrimsonMapSecretKey123!";
    const encryptedPassword = CryptoJS.AES.encrypt(password, secretKey).toString();

    await addDoc(collection(db, "Users"), {
      firstName,
      middleInitial,
      lastName,
      contactNumber,
      email,
      password: encryptedPassword,
      created_at: serverTimestamp(),
    });

    alert("✅ User added successfully!");
    addUserForm.reset();
    addUserModal.style.display = "none";
  } catch (error) {
    console.error("Error adding user:", error);
    alert("❌ Failed to add user. Please try again.");
  }
});




document.addEventListener("DOMContentLoaded", () => {
  const emailInput = document.querySelector(".account-section #accountEmail");
  const currentUser = JSON.parse(sessionStorage.getItem("currentUser"));

  if (currentUser && currentUser.email && emailInput) {
    emailInput.value = currentUser.email;
  } else if (!currentUser) {
    window.location.href = "/html/login.html";
  }
});

// --- Sidebar collapse: wrap labels and enable toggle (same UX as reports page)
function wrapSidebarLabelsAccount() {
  const anchors = document.querySelectorAll('.left .sidebar ul li a');
  anchors.forEach((a) => {
    if (a.querySelector('.sidebar-label')) return;
    const nodes = Array.from(a.childNodes).filter(n => n.nodeType === Node.TEXT_NODE && n.textContent.trim().length);
    if (nodes.length === 0) return;
    const span = document.createElement('span');
    span.className = 'sidebar-label';
    nodes.forEach(n => span.appendChild(n));
    a.appendChild(span);
  });
}

document.addEventListener('DOMContentLoaded', () => {
  // prepare sidebar labels
  wrapSidebarLabelsAccount();

  const menuIcon = document.querySelector('.menu-icon');
  const leftPane = document.querySelector('.left');
  if (!menuIcon || !leftPane) return;

  try {
    const collapsed = localStorage.getItem('sidebarCollapsed');
    if (collapsed === 'true') leftPane.classList.add('collapsed');
  } catch (e) {}

  menuIcon.addEventListener('click', () => {
    const isCollapsed = leftPane.classList.toggle('collapsed');
    menuIcon.style.transition = 'transform 200ms ease';
    menuIcon.style.transform = isCollapsed ? 'rotate(90deg)' : 'rotate(0deg)';
    try { localStorage.setItem('sidebarCollapsed', isCollapsed ? 'true' : 'false'); } catch(e) {}
  });
});
