import { firebaseConfig } from "../firebaseConfig.js";
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.11.1/firebase-app.js";
import { getFirestore, doc, getDoc, updateDoc, increment, setDoc } 
    from "https://www.gstatic.com/firebasejs/10.11.1/firebase-firestore.js";

// Initialize Firebase
const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// SELECT DOM ELEMENTS
const downloadBtn = document.getElementById("downloadBtn");
const countSpan = document.getElementById("downloadCount");

// DEFINE countRef
const countRef = doc(db, "AppStats", "Downloads");

// HELPER: Format numbers
function formatCount(num) {
    if (num >= 1_000_000) {
        return (num / 1_000_000).toFixed(1) + "m";
    } else if (num >= 1_000) {
        return (num / 1_000).toFixed(2).replace(/\.?0+$/, '') + "k";
    } else {
        return num.toString();
    }
}

// Load the current count
async function loadCount() {
    const snap = await getDoc(countRef);
    if (snap.exists()) {
        const count = snap.data().apk_downloads;
        countSpan.textContent = `(${formatCount(count)} downloads)`;
    } else {
        countSpan.textContent = "(0 downloads)";
    }
}

// Increment on click
downloadBtn.addEventListener("click", async () => {
    try {
        await updateDoc(countRef, {
            apk_downloads: increment(1)
        });
    } catch (e) {
        // Document does not exist yet → create it
        await setDoc(countRef, { apk_downloads: 1 });
    }

    // Update UI instantly
    const snap = await getDoc(countRef);
    const count = snap.exists() ? snap.data().apk_downloads : 1;
    countSpan.textContent = `(${formatCount(count)} downloads)`;
});

// RUN LAST
loadCount();
