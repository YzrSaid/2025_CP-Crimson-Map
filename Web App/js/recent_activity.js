
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore,
    collection,
    getDocs,
    query,
    orderBy
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";
import { firebaseConfig } from "../firebaseConfig.js";


const app = initializeApp(firebaseConfig);
const db = getFirestore(app);


async function loadFromFirestore() {
    const q = query(collection(db, "ActivityLogs"), orderBy("timestamp", "asc"));
    const querySnapshot = await getDocs(q);

    const results = [];
    querySnapshot.forEach(docSnap => {
        results.push({ id: docSnap.id, ...docSnap.data() });
    });

    
    results.sort((a, b) => getTimestampValue(a.timestamp) - getTimestampValue(b.timestamp));

    return results;
}


async function loadFromJson() {
    const res = await fetch("../assets/firestore/ActivityLogs.json");
    const data = await res.json();

    
    data.sort((a, b) => getTimestampValue(a.timestamp) - getTimestampValue(b.timestamp));

    return data;
}


function getTimestampValue(ts) {
    if (!ts) return 0;

    
    if (typeof ts.toDate === "function") return ts.toDate().getTime();

    
    if (ts.seconds !== undefined) return ts.seconds * 1000 + Math.floor(ts.nanoseconds / 1e6);

    
    if (ts._seconds !== undefined) return ts._seconds * 1000 + Math.floor((ts._nanoseconds || 0) / 1e6);

    return 0;
}


function formatTimestamp(ts) {
    if (!ts) return "-";
    const ms = getTimestampValue(ts);
    if (!ms) return "-";
    return formatDate(new Date(ms));
}

function formatDate(d) {
    const dateStr = d.toLocaleDateString("en-CA"); 
    const timeStr = d.toLocaleTimeString("en-US", {
        hour: "numeric",
        minute: "2-digit",
        hour12: true
    });
    return `${dateStr}<br>${timeStr}`;
}


async function renderActivityLogsTable() {
    const tbody = document.querySelector(".activity-table tbody");
    if (!tbody) return;

    
    tbody.innerHTML = `
        <tr class="table-spinner">
            <td colspan="5">
                <div class="spinner"></div>
            </td>
        </tr>
    `;

    try {
        let logs;
        if (navigator.onLine) {
            logs = await loadFromFirestore();
        } else {
            logs = await loadFromJson();
        }

        
        tbody.innerHTML = "";

        let counter = 1;
        logs.forEach(data => {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${counter++}</td>
                <td>${formatTimestamp(data.timestamp)}</td>
                <td>${data.activity || "-"}</td>
                <td>${data.item || "-"}</td>
                <td>${data.description || "-"}</td>
            `;
            tbody.appendChild(tr);
        });

        
        if (logs.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="5" style="text-align:center; padding:20px;">No activity logs found.</td>
                </tr>
            `;
        }

    } catch (err) {
        console.error("Error loading activity logs: ", err);
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align:center; padding:20px; color:red;">
                    Failed to load activity logs.
                </td>
            </tr>
        `;
    }
}



window.addEventListener("online", renderActivityLogsTable);
window.addEventListener("offline", renderActivityLogsTable);


document.addEventListener("DOMContentLoaded", renderActivityLogsTable);
