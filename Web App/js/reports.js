import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore,
    collection,
    getDocs,
    doc,
    updateDoc,
    onSnapshot,
    query,
    orderBy
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

import { firebaseConfig } from "../firebaseConfig.mjs";

const app = initializeApp(firebaseConfig);
const db = getFirestore(app);

// Status color mapping
const statusColors = {
    "Pending": { bg: "#fffbe6", text: "#b8860b", border: "#f0c36d" },
    "In Progress": { bg: "#e6f7e6", text: "#2d7a2d", border: "#66bb6a" },
    "Handled": { bg: "#e3f2fd", text: "#1976d2", border: "#64b5f6" },
    "Rejected": { bg: "#ffebee", text: "#c62828", border: "#ef5350" }
};

// Format timestamp to readable date
function formatDate(timestamp) {
    if (!timestamp) return "N/A";
    if (timestamp.toDate) {
        return timestamp.toDate().toLocaleString("en-US", {
            year: "numeric",
            month: "short",
            day: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        });
    }
    return "N/A";
}

// Load and display reports
async function loadReports() {
    const tbody = document.querySelector(".reports-table tbody");
    if (!tbody) return;

    try {
        // Clear existing rows
        tbody.innerHTML = "";

        // Query reports ordered by date (newest first)
        const reportsQuery = query(
            collection(db, "UserReports"),
            orderBy("createdAt", "desc")
        );

        const snapshot = await getDocs(reportsQuery);

        if (snapshot.empty) {
            tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; color:#999;">No reports yet</td></tr>`;
            return;
        }

        let rowNumber = 1;

        snapshot.forEach((doc) => {
            const report = doc.data();
            const reportId = doc.id;

            // Create row
            const tr = document.createElement("tr");
            tr.dataset.reportId = reportId;

            // Format date
            const formattedDate = formatDate(report.createdAt || report.date);

            // Get status color
            const statusColor = statusColors[report.status] || statusColors["Pending"];

            tr.innerHTML = `
                <td>${rowNumber}</td>
                <td>${report.affected || "N/A"}</td>
                <td>${report.type || "N/A"}</td>
                <td>${report.issue || "N/A"}</td>
                <td>${formattedDate}</td>
                <td class="status-cell">
                    <span class="status" style="color: ${statusColor.text}; background-color: ${statusColor.bg}; border: 1px solid ${statusColor.border};">
                        ${report.status || "Pending"}
                    </span>
                </td>
                <td class="actions">
                    <div class="menu-wrapper">
                        <button class="menu-btn" data-report-id="${reportId}">⋮</button>
                        <div class="dropdown-menu">
                            <div class="dropdown-item" data-status="Pending">Pending</div>
                            <div class="dropdown-item" data-status="In Progress">In Progress</div>
                            <div class="dropdown-item" data-status="Handled">Handled</div>
                            <div class="dropdown-item" data-status="Rejected">Reject</div>
                        </div>
                    </div>
                </td>
            `;

            tbody.appendChild(tr);
            rowNumber++;
        });

        // Attach event listeners to dropdown menus
        setupMenuListeners();

    } catch (err) {
        console.error("Error loading reports:", err);
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; color:red;">Error loading reports</td></tr>`;
    }
}

function setupMenuListeners() { 
    document.querySelectorAll(".menu-btn").forEach((btn) => { 
        btn.addEventListener("click", (e) => { 
            e.stopPropagation();

            const wrapper = btn.closest(".menu-wrapper");
            const dropdown = wrapper.querySelector(".dropdown-menu");
            const isOpen = dropdown.classList.contains("active");

            // Close all other dropdowns
            document.querySelectorAll(".dropdown-menu").forEach((menu) => {
                menu.classList.remove("active");
            });

            // Toggle this dropdown only if it was closed
            if (!isOpen) {
                dropdown.classList.add("active");
            }
        });
    });

    document.querySelectorAll(".dropdown-item").forEach((item) => {
        item.addEventListener("click", async (e) => {
            const newStatus = e.target.dataset.status;
            const dropdown = e.target.closest(".dropdown-menu");
            const btn = dropdown.closest(".menu-wrapper").querySelector(".menu-btn");
            const reportId = btn.dataset.reportId;

            // Update status in Firebase
            await updateReportStatus(reportId, newStatus);

            dropdown.classList.remove("active");
        });
    });

    // Close dropdown when clicking outside
    document.addEventListener("click", (e) => {
        if (!e.target.closest(".menu-wrapper")) {
            document.querySelectorAll(".dropdown-menu").forEach((menu) => {
                menu.classList.remove("active");
            });
        } 
    }); 
}


// Update report status in Firestore
async function updateReportStatus(reportId, newStatus) {
    try {
        const reportRef = doc(db, "UserReports", reportId);
        await updateDoc(reportRef, {
            status: newStatus
        });

        // Reload reports to reflect changes
        await loadReports();
    } catch (err) {
        console.error("Error updating report status:", err);
        alert("Failed to update status. Please try again.");
    }
}

// Ensure all dropdowns are closed on load
function ensureDropdownsClosed() {
    const menus = document.querySelectorAll(".dropdown-menu");
    menus.forEach((menu) => {
        menu.classList.remove("active");
    });
}

// Load reports on page load
document.addEventListener("DOMContentLoaded", () => {
    // Wrap sidebar anchor text into a span so we can hide labels when collapsed
    wrapSidebarLabels();
    ensureDropdownsClosed();
    loadReports();
});

// Sidebar collapse toggle
(() => {
    // keep behavior on DOMContentLoaded to ensure DOM exists
    document.addEventListener('DOMContentLoaded', () => {
        const menuIcon = document.querySelector('.menu-icon');
        const leftPane = document.querySelector('.left');
        if (!menuIcon || !leftPane) return;

        // Restore previous state if stored
        try {
            const collapsed = localStorage.getItem('sidebarCollapsed');
            if (collapsed === 'true') leftPane.classList.add('collapsed');
        } catch (e) {
            // ignore
        }

        menuIcon.addEventListener('click', () => {
            const isCollapsed = leftPane.classList.toggle('collapsed');
            // simple icon animation (rotate)
            menuIcon.style.transition = 'transform 200ms ease';
            menuIcon.style.transform = isCollapsed ? 'rotate(90deg)' : 'rotate(0deg)';
            try { localStorage.setItem('sidebarCollapsed', isCollapsed ? 'true' : 'false'); } catch(e) {}
        });
    });
})();

// Wrap sidebar anchor text nodes in a span.sidebar-label so CSS can hide them cleanly
function wrapSidebarLabels() {
    const anchors = document.querySelectorAll('.left .sidebar ul li a');
    anchors.forEach((a) => {
        // if already wrapped, skip
        if (a.querySelector('.sidebar-label')) return;

        // collect text nodes (exclude icon elements)
        const nodes = Array.from(a.childNodes).filter(n => n.nodeType === Node.TEXT_NODE && n.textContent.trim().length);
        if (nodes.length === 0) return;

        const span = document.createElement('span');
        span.className = 'sidebar-label';
        // move text nodes into span
        nodes.forEach(n => span.appendChild(n));
        a.appendChild(span);
    });
}
