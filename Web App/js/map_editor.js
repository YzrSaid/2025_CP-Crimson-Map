
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.4/firebase-app.js";
import {
    getFirestore, collection, addDoc, getDocs, query, orderBy, where, updateDoc, doc, getDoc, arrayUnion, writeBatch, deleteDoc, setDoc, limit
} from "https://www.gstatic.com/firebasejs/10.12.4/firebase-firestore.js";

import { firebaseConfig } from "../firebaseConfig.js";


const app = initializeApp(firebaseConfig);
const db = getFirestore(app);





function showNodeModal() {
    document.getElementById('addNodeModal').style.display = 'flex';
    generateNextNodeId();
    populateInfraDropdown();
    populateIndoorInfraDropdown(); 
    populateCampusDropdown();
}
function hideNodeModal() {
    document.getElementById('addNodeModal').style.display = 'none';
    document.getElementById("nodeName").value = "";
    document.getElementById("latitude").value = "";
    document.getElementById("longitude").value = "";
}
window.openNodeModal = showNodeModal;
window.closeNodeModal = hideNodeModal;


async function generateNextNodeId() {
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect ? mapSelect.value : null;
    if (!mapId) return;

    
    const mapDocRef = doc(db, "MapVersions", String(mapId));
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const currentVersion = mapDocSnap.data().current_version || "v1.0.0";
    const versionRef = doc(db, "MapVersions", String(mapId), "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) return;

    const nodes = Array.isArray(versionSnap.data().nodes) ? versionSnap.data().nodes : [];

    let maxNum = 0;
    nodes.forEach(node => {
        if (node.node_id) {
            const num = parseInt(node.node_id.replace("ND-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `ND-${String(maxNum + 1).padStart(3, "0")}`;
    document.getElementById("nodeId").value = nextId;
}


async function loadBuildingsDropdownForNode() {
    const buildingSelect = document.getElementById("linkedBuilding");
    if (!buildingSelect) return;

    buildingSelect.innerHTML = `<option value="">Select a building</option>`;

    try {
        const q = query(collection(db, "Buildings"), orderBy("createdAt", "asc"));
        const snapshot = await getDocs(q);

        snapshot.forEach(doc => {
            const data = doc.data();
            if (data.building_id && data.name) {
                const option = document.createElement("option");
                option.value = data.building_id;
                option.textContent = `${data.building_id} - ${data.name}`;
                buildingSelect.appendChild(option);
            }
        });
    } catch (err) {
        console.error("Error loading buildings into dropdown:", err);
    }
}


async function loadBuildingsDropdownById(selectId) {
    const buildingSelect = document.getElementById(selectId);
    if (!buildingSelect) return;

    buildingSelect.innerHTML = `<option value="">Select a building</option>`;

    try {
        const q = query(collection(db, "Buildings"), orderBy("createdAt", "asc"));
        const snapshot = await getDocs(q);

        snapshot.forEach(docSnap => {
            const data = docSnap.data();
            if (data.building_id && data.name) {
                const option = document.createElement("option");
                option.value = data.building_id;
                option.textContent = `${data.building_id} - ${data.name}`;
                buildingSelect.appendChild(option);
            }
        });
    } catch (err) {
        console.error("Error loading buildings into dropdown:", err);
    }
}


async function populateInfraDropdown(selectId = "relatedInfra") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select infrastructure</option>`;

    const q = query(collection(db, "Infrastructure"));
    const snapshot = await getDocs(q);

    
    const infraList = [];
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.infra_id && data.name) {
            infraList.push({
                id: data.infra_id,
                name: data.name
            });
        }
    });

    
    infraList.sort((a, b) => a.name.localeCompare(b.name));

    
    infraList.forEach(infra => {
        const option = document.createElement("option");
        option.value = infra.id;
        option.textContent = infra.name;
        select.appendChild(option);
    });
}



async function populateRoomDropdown(selectId = "relatedRoom") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select room</option>`;
    const q = query(collection(db, "Rooms"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.room_id && data.name) {
            const option = document.createElement("option");
            option.value = data.room_id;
            option.textContent = data.name;
            select.appendChild(option);
        }
    });
}


async function populateCampusDropdown(selectId = "campusDropdown") {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select campus</option>`;
    const q = query(collection(db, "Campus"));
    const snapshot = await getDocs(q);
    snapshot.forEach(doc => {
        const data = doc.data();
        if (data.campus_id && data.campus_name) {
            const option = document.createElement("option");
            option.value = data.campus_id;
            option.textContent = data.campus_name;
            select.appendChild(option);
        }
    });
}








async function renderNodesTable() {
    const tbody = document.querySelector(".nodetbl tbody");
    if (!tbody) return;

    
    tbody.innerHTML = `
        <tr class="loading-row">
            <td colspan="9" style="text-align:center; padding:20px;">
                <div class="spinner"></div>
                <span class="loading-text">Loading nodes...</span>
            </td>
        </tr>
    `;

    try {
        // Get the currently selected map and campus from UI
        const mapSelect = document.getElementById("mapSelect");
        const campusSelect = document.getElementById("campusSelect");
        const versionSelect = document.getElementById("versionSelect");
        
        const selectedMapId = mapSelect ? mapSelect.value : null;
        const selectedCampus = campusSelect ? campusSelect.value : null;
        const selectedVersion = versionSelect ? versionSelect.value : null;
        
        if (!selectedMapId || !selectedCampus || !selectedVersion) {
            tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; color:orange;">Please select a map, campus, and version.</td></tr>`;
            return;
        }

        let nodes = [];
        let infra = [];
        let rooms = [];
        let indoorInfras = [];
        let campuses = [];

        if (navigator.onLine) {
            
            const [infraSnap, roomSnap, indoorSnap, campusSnap] = await Promise.all([
                getDocs(collection(db, "Infrastructure")),
                getDocs(collection(db, "Rooms")),
                getDocs(collection(db, "IndoorInfrastructure")),
                getDocs(collection(db, "Campus"))
            ]);

            infra = infraSnap.docs.map(d => d.data());
            rooms = roomSnap.docs.map(d => d.data());
            indoorInfras = indoorSnap.docs.map(d => d.data());
            campuses = campusSnap.docs.map(d => d.data());

            // Load ONLY the selected map version's data
            const versionRef = doc(db, "MapVersions", selectedMapId, "versions", selectedVersion);
            const versionSnap = await getDoc(versionRef);
            if (versionSnap.exists()) {
                const versionData = versionSnap.data();
                const mapNodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
                // Filter by selected campus only
                nodes = mapNodes.filter(n => !n.is_deleted && n.campus_id === selectedCampus);
            }
        } else {
            
            const [nodesRes, infraRes, roomsRes, indoorRes, campusesRes] = await Promise.all([
                fetch("../assets/firestore/MapVersions.json"),
                fetch("../assets/firestore/Infrastructure.json"),
                fetch("../assets/firestore/Rooms.json"),
                fetch("../assets/firestore/IndoorInfrastructure.json"),
                fetch("../assets/firestore/Campus.json")
            ]);

            const mapVersions = await nodesRes.json();
            infra = (await infraRes.json()).filter(i => !i.is_deleted);
            rooms = (await roomsRes.json()).filter(r => !r.is_deleted);
            indoorInfras = (await indoorRes.json()).filter(r => !r.is_deleted);
            campuses = (await campusesRes.json()).filter(c => !c.is_deleted);

            // Load ONLY the selected map version's data (offline mode)
            const mapData = mapVersions.find(m => m.id === selectedMapId);
            if (mapData) {
                const version = mapData.versions.find(v => v.id === selectedVersion);
                if (version) {
                    const mapNodes = Array.isArray(version.nodes) ? version.nodes : [];
                    // Filter by selected campus only
                    nodes = mapNodes.filter(n => !n.is_deleted && n.campus_id === selectedCampus);
                }
            }
        }

        
        const infraMap = Object.fromEntries(infra.map(i => [i.infra_id, i.name]));
        const roomMap = Object.fromEntries(rooms.map(r => [r.room_id, r.name]));
        const indoorInfraMap = Object.fromEntries(indoorInfras.map(r => [r.room_id, r.name]));
        const campusMap = Object.fromEntries(campuses.map(c => [c.campus_id, c.campus_name]));

        
        nodes.sort((a, b) => (a.created_at?.seconds || 0) - (b.created_at?.seconds || 0));

        
        tbody.innerHTML = "";
        nodes.forEach(data => {
            const coords = (data.latitude && data.longitude) ? `${data.latitude}, ${data.longitude}` : "-";
            const infraName = data.related_infra_id ? (infraMap[data.related_infra_id] || data.related_infra_id) : "-";

            let roomName = "-";
            if (data.related_room_id) roomName = indoorInfraMap[data.related_room_id] || roomMap[data.related_room_id] || data.related_room_id;

            const campusName = data.campus_id ? (campusMap[data.campus_id] || data.campus_id) : "-";

            let indoorOutdoor = "Outdoor";
            if (data.indoor) {
                indoorOutdoor = `Indoor (Floor: ${data.indoor.floor ?? "-"}, X: ${data.indoor.x ?? "-"}, Y: ${data.indoor.y ?? "-"})`;
            }

            const type = data.type ? data.type.charAt(0).toUpperCase() + data.type.slice(1) : "-";

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.node_id}</td>
                <td>${data.name}</td>
                <td>${type}</td>
                <td>${coords}</td>
                <td>${infraName}</td>
                <td>${roomName}</td>
                <td>${indoorOutdoor}</td>
                <td>${campusName}</td>
                <td class="actions">
                    <i class="fas fa-edit"></i>
                    <i class="fas fa-trash" data-node-id="${data.node_id}"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupNodeDeleteHandlers();
    } catch (err) {
        console.error("Error loading nodes:", err);
        tbody.innerHTML = `<tr><td colspan="9" style="text-align:center; color:red;">Error loading nodes</td></tr>`;
    }
}




















































































































document.addEventListener("DOMContentLoaded", function() {
    const typeSelect = document.getElementById("nodeType");
    let typeInput = null;

    typeSelect.addEventListener("change", function() {
        if (this.value === "other") {
            typeInput = document.createElement("input");
            typeInput.type = "text";
            typeInput.id = "nodeType";
            typeInput.placeholder = "Enter type";
            typeInput.classList.add("custom-input");
            this.parentNode.replaceChild(typeInput, this);

            typeInput.addEventListener("blur", function() {
                if (typeInput.value.trim() === "") {
                    typeInput.parentNode.replaceChild(typeSelect, typeInput);
                    typeSelect.value = "";
                }
            });
        }
    });
});












async function populateIndoorInfraDropdown(selectId = "relatedIndoorInfra", includeId = null) {
    const select = document.getElementById(selectId);
    if (!select) return;
    select.innerHTML = `<option value="">Select Indoor Infra</option>`;

    try {
        // Load all indoor infra
        let indoorList = [];
        if (navigator.onLine) {
            const q = query(collection(db, "IndoorInfrastructure"));
            const snapshot = await getDocs(q);
            snapshot.forEach(docSnap => {
                const data = docSnap.data();
                if (data.room_id && data.name) {
                    indoorList.push({ id: data.room_id, name: data.name });
                }
            });

            // collect used room ids from current map versions
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            const usedRoomIds = new Set();
            for (const mapDoc of mapsSnap.docs) {
                const mapData = mapDoc.data();
                const currentVersion = mapData.current_version;
                if (!currentVersion) continue;
                const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
                const versionSnap = await getDoc(versionRef);
                if (!versionSnap.exists()) continue;
                const nodes = versionSnap.data().nodes || [];
                nodes.forEach(n => {
                    if (n.related_room_id) usedRoomIds.add(String(n.related_room_id));
                });
            }

            // filter out indoor infra that already have a node, but keep includeId if provided
            const filtered = indoorList.filter(item => {
                if (!item || !item.id) return false;
                if (includeId && String(includeId) === String(item.id)) return true;
                return !usedRoomIds.has(String(item.id));
            });

            filtered.sort((a, b) => a.name.localeCompare(b.name));
            filtered.forEach(item => {
                const option = document.createElement("option");
                option.value = item.id;
                option.textContent = item.name;
                select.appendChild(option);
            });
        } else {
            // offline fallback using static assets
            const indoorRes = await fetch("../assets/firestore/IndoorInfrastructure.json").then(r => r.json());
            const mapsRes = await fetch("../assets/firestore/MapVersions.json").then(r => r.json());

            const usedRoomIds = new Set();
            (mapsRes || []).forEach(map => {
                const currentVersionId = map.current_version;
                const version = (map.versions || []).find(v => v.id === currentVersionId);
                if (!version) return;
                (version.nodes || []).forEach(n => {
                    if (n.related_room_id) usedRoomIds.add(String(n.related_room_id));
                });
            });

            const items = (indoorRes || []).filter(d => d.room_id && d.name)
                .filter(d => {
                    if (includeId && String(includeId) === String(d.room_id)) return true;
                    return !usedRoomIds.has(String(d.room_id));
                })
                .map(d => ({ id: d.room_id, name: d.name }));

            items.sort((a, b) => a.name.localeCompare(b.name));
            items.forEach(item => {
                const option = document.createElement("option");
                option.value = item.id;
                option.textContent = item.name;
                select.appendChild(option);
            });
        }
    } catch (err) {
        console.error("Error loading indoor infrastructure into dropdown:", err);
    }
}








document.addEventListener("DOMContentLoaded", function() {
    const nodeTypeSelect = document.getElementById("nodeType");
    const relatedInfraSelect = document.getElementById("relatedInfra");
    const relatedIndoorInfraSelect = document.getElementById("relatedIndoorInfra");
    const indoorDetails = document.getElementById("indoorDetails");
    const coordinatesBlock = Array.from(document.querySelectorAll(".form-group"))
        .find(group => group.querySelector("label")?.textContent.trim() === "Coordinates");

    
    nodeTypeSelect.value = "";

    
    indoorDetails.style.display = "none";

    nodeTypeSelect.addEventListener("change", function() {
        const type = this.value;

        if (type === "indoorInfra") {
            relatedInfraSelect.disabled = true;
            relatedInfraSelect.classList.add("disabled");
            relatedIndoorInfraSelect.disabled = false;
            relatedIndoorInfraSelect.classList.remove("disabled");
            indoorDetails.style.display = "block"; 

            
            if (coordinatesBlock) coordinatesBlock.style.display = "none";

        } else if (type === "infrastructure") {
            relatedInfraSelect.disabled = false;
            relatedInfraSelect.classList.remove("disabled");
            relatedIndoorInfraSelect.disabled = true;
            relatedIndoorInfraSelect.classList.add("disabled");
            indoorDetails.style.display = "none"; 

            
            if (coordinatesBlock) coordinatesBlock.style.display = "";
            
        } else if (type === "intermediate" || type === "barrier") {
            relatedInfraSelect.disabled = true;
            relatedInfraSelect.classList.add("disabled");
            relatedIndoorInfraSelect.disabled = true;
            relatedIndoorInfraSelect.classList.add("disabled");
            indoorDetails.style.display = "none"; 

            
            if (coordinatesBlock) coordinatesBlock.style.display = "";

        } else {
            relatedInfraSelect.disabled = false;
            relatedInfraSelect.classList.remove("disabled");
            relatedIndoorInfraSelect.disabled = false;
            relatedIndoorInfraSelect.classList.remove("disabled");
            indoorDetails.style.display = "none";

            
            if (coordinatesBlock) coordinatesBlock.style.display = "";
        }
    });
});








let pendingNodeData = null; 

document.getElementById("nodeForm").addEventListener("submit", async (e) => {
    e.preventDefault();

    
    const parseNumberOrNull = (val) => {
        const num = parseFloat(val);
        return isNaN(num) ? null : num;
    };

    const stringOrNull = (val) => val && val.trim() !== "" ? val : null;

    
    const nodeId = document.getElementById("nodeId").value;
    const nodeName = stringOrNull(document.getElementById("nodeName").value);
    const latitude = parseNumberOrNull(document.getElementById("latitude").value);
    const longitude = parseNumberOrNull(document.getElementById("longitude").value);
    const typeEl = document.getElementById("nodeType");
    const typeValue = typeEl ? typeEl.value : "";

    const relatedIndoorInfraId = stringOrNull(document.getElementById("relatedIndoorInfra").value);
    const campusId = stringOrNull(document.getElementById("campusDropdown").value);

    let type = null;
    let indoor = null;

    if (typeValue === "indoorInfra") {
        type = "indoorinfra";
        indoor = {
            floor: stringOrNull(document.getElementById("floor").value),
            x: parseNumberOrNull(document.getElementById("xCoord").value),
            y: parseNumberOrNull(document.getElementById("yCoord").value)
        };

        if (!indoor.floor && indoor.x === null && indoor.y === null) {
            indoor = null;
        }
    } else if (typeValue === "infrastructure") {
        type = "infrastructure";
        indoor = null;
    } else if (typeValue === "barrier") {
        type = "barrier";
        indoor = null;
    } else if (typeValue === "intermediate") {
        type = "intermediate";
        indoor = null;
    }


    
    let xCoord = null, yCoord = null;
    if (latitude !== null && longitude !== null) {
        const origin = { lat: 6.913341, lng: 122.063693 };
        function latLngToXY(lat, lng, origin) {
            const R = 6371000;
            const dLat = (lat - origin.lat) * Math.PI / 180;
            const dLng = (lng - origin.lng) * Math.PI / 180;
            const x = dLng * Math.cos(origin.lat * Math.PI / 180) * R;
            const y = dLat * R;
            return { x, y };
        }
        const { x, y } = latLngToXY(latitude, longitude, origin);
        xCoord = x;
        yCoord = y;
    }

    
    // Read the raw related infra select value; fallback to null when the node is indoor
    const relatedInfraIdRaw = stringOrNull(document.getElementById("relatedInfra")?.value);
    const relatedInfraId = (type === "indoorinfra") ? null : relatedInfraIdRaw;

    // build pendingNodeData (use serverTimestamp if you want server time; kept new Date() to match original)
    pendingNodeData = {
        node_id: nodeId,
        name: nodeName,
        latitude,
        longitude,
        x_coordinate: xCoord,
        y_coordinate: yCoord,
        type,
        related_infra_id: relatedInfraId,
        related_room_id: relatedIndoorInfraId,
        indoor,
        is_active: true,
        campus_id: campusId,
        created_at: new Date()
    };

    document.getElementById("nodeSaveModal").style.display = "flex";
});


document.getElementById("closeNodeSaveModal").addEventListener("click", () => {
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

document.getElementById("overwriteNodeBtn").addEventListener("click", async () => {
    await saveNode("overwrite");
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

document.getElementById("newVersionNodeBtn").addEventListener("click", async () => {
    await saveNode("newVersion");
    pendingNodeData = null;
    document.getElementById("nodeSaveModal").style.display = "none";
});

async function saveNode(option) {
    if (!pendingNodeData) return;

    try {
        const campusId = pendingNodeData.campus_id;

        
        const mapsQuery = query(
            collection(db, "MapVersions"),
            where("campus_included", "array-contains", campusId)
        );
        const mapsSnapshot = await getDocs(mapsQuery);

        if (mapsSnapshot.empty) {
            showModal('error', 'No map found for this campus.');
            return;
        }

        const mapDoc = mapsSnapshot.docs[0];
        const mapDocId = mapDoc.id;
        const mapData = mapDoc.data();
        const currentVersion = mapData.current_version || "v1.0.0";

        
        const versionRef = doc(db, "MapVersions", mapDocId, "versions", currentVersion);
        const versionSnap = await getDoc(versionRef);

        let oldNodes = [];
        let oldEdges = [];

        if (versionSnap.exists()) {
            const versionData = versionSnap.data();
            oldNodes = versionData.nodes || [];
            oldEdges = versionData.edges || [];
        }

        if (option === "overwrite") {
            const updatedNodes = oldNodes.filter(n => n.node_id !== pendingNodeData.node_id);
            updatedNodes.push(pendingNodeData);

            await updateDoc(versionRef, { nodes: updatedNodes });

            showModal('success', `Node ${pendingNodeData.node_id} added/updated in current version ${currentVersion}`);
        } 
        else if (option === "newVersion") {
            let [major, minor, patch] = currentVersion.slice(1).split(".").map(Number);
            if (patch < 99) patch += 1;
            else { patch = 0; minor += 1; }

            const newVersion = `v${major}.${minor}.${patch}`;
            const migratedNodes = oldNodes.map(n => ({ ...n }));
            migratedNodes.push({ ...pendingNodeData });
            const migratedEdges = oldEdges.map(e => ({ ...e }));

            await setDoc(doc(db, "MapVersions", mapDocId, "versions", newVersion), {
                nodes: migratedNodes,
                edges: migratedEdges
            });

            await updateDoc(doc(db, "MapVersions", mapDocId), { 
                current_version: newVersion,
                current_version_updated: true,
            });

            showModal('success', 'New version created successfully!');
        }

        
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, { infrastructure_updated: true });

        
        const allCampuses = mapData.campus_included || [];

        let allNodes = [];
        for (const campId of allCampuses) {
            const versionQuery = query(
                collection(db, "MapVersions", mapDocId, "versions")
            );
            const versionDocs = await getDocs(versionQuery);
            
            versionDocs.forEach(vDoc => {
                const vData = vDoc.data();
                const campusNodes = (vData.nodes || []).filter(n => n.campus_id === campId);
                allNodes.push(...campusNodes);
            });
        }

        
        const getGeographicCenter = (nodes) => {
            if (!nodes.length) return [6.9130, 122.0630];
            let x = 0, y = 0, z = 0;
            nodes.forEach(n => {
                if (!n.latitude || !n.longitude) return;
                const latRad = parseFloat(n.latitude) * Math.PI / 180;
                const lonRad = parseFloat(n.longitude) * Math.PI / 180;
                x += Math.cos(latRad) * Math.cos(lonRad);
                y += Math.cos(latRad) * Math.sin(lonRad);
                z += Math.sin(latRad);
            });
            const total = nodes.length;
            x /= total; y /= total; z /= total;
            const lon = Math.atan2(y, x);
            const hyp = Math.sqrt(x * x + y * y);
            const lat = Math.atan2(z, hyp);
            return [lat * 180 / Math.PI, lon * 180 / Math.PI];
        };

        const [centerLat, centerLng] = getGeographicCenter(allNodes);

        
        const mapsCollection = collection(db, "Maps");
        const mapsQueryRef = query(mapsCollection, where("map_id", "==", mapDocId));
        const mapsDocs = await getDocs(mapsQueryRef);

        if (!mapsDocs.empty) {
            const mapRef = mapsDocs.docs[0].ref;
            await updateDoc(mapRef, {
                center_lat: centerLat,
                center_lng: centerLng,
                updatedAt: new Date()
            });
            console.log(`Map center updated for ${mapDocId}:`, centerLat, centerLng);
        }

        
        document.getElementById("nodeForm").reset();
        document.getElementById("indoorDetails").style.display = "none";
        generateNextNodeId();
        renderNodesTable();
        pendingNodeData = null;
        document.getElementById("nodeSaveModal").style.display = "none";

    } catch (err) {
        console.error(err);
        showModal('error', 'Failed to save node. Please try again.');
    }
}



function handleIndoorInfraSelection({
  selectId = "relatedIndoorInfra",
  nameInputId = "nodeName",
  relatedInfraSelectId = "relatedInfra", // still accepted but will not be written to
  campusSelectId = "campusDropdown"
} = {}) {
  const select = document.getElementById(selectId);
  if (!select) return;

  let lastToken = 0;

  select.addEventListener("change", async () => {
    const token = ++lastToken;
    const roomId = select.value;
    if (!roomId) return;

    const nameInput = document.getElementById(nameInputId);
    const campusSelect = document.getElementById(campusSelectId);

    const setNameIfAllowed = (val) => {
      if (!nameInput) return;
      const wasAuto = nameInput.dataset.autofilled === "true";
      if (!nameInput.value || wasAuto) {
        nameInput.value = val || "";
        nameInput.dataset.autofilled = val ? "true" : "false";
      }
      if (!nameInput._autofillListenerAdded) {
        nameInput.addEventListener("input", () => {
          if (nameInput.dataset.autofilled === "true") nameInput.dataset.autofilled = "false";
        });
        nameInput._autofillListenerAdded = true;
      }
    };

    try {
      if (navigator.onLine) {
        // Prefer limit(1) for cost and speed where available
        const indoorQ = query(collection(db, "IndoorInfrastructure"), where("room_id", "==", roomId));
        const indoorSnap = await getDocs(indoorQ);
        let indoorDocData = null;
        if (!indoorSnap.empty) {
          indoorDocData = indoorSnap.docs[0].data();
        } else {
          const roomsQ = query(collection(db, "Rooms"), where("room_id", "==", roomId));
          const rSnap = await getDocs(roomsQ);
          if (!rSnap.empty) indoorDocData = rSnap.docs[0].data();
        }

        if (token !== lastToken) return;

        if (indoorDocData) {
          setNameIfAllowed(indoorDocData.name || indoorDocData.room_name || "");

          // compute campusId - prefer indoorDocData.campus_id then Rooms then Infrastructure
          let campusId = indoorDocData.campus_id || null;

          if (!campusId) {
            try {
              const roomsQ2 = query(collection(db, "Rooms"), where("room_id", "==", roomId), limit(1));
              const roomsSnap2 = await getDocs(roomsQ2);
              if (roomsSnap2 && !roomsSnap2.empty) {
                const r = roomsSnap2.docs[0].data();
                if (r && r.campus_id) campusId = r.campus_id;
              }
            } catch (e) { /* ignore */ }
          }

          if (!campusId && indoorDocData.infra_id) {
            try {
              const infraQ = query(collection(db, "Infrastructure"), where("infra_id", "==", indoorDocData.infra_id), limit(1));
              const infraSnap = await getDocs(infraQ);
              if (!infraSnap.empty) {
                const infraData = infraSnap.docs[0].data();
                if (infraData && infraData.campus_id) campusId = infraData.campus_id;
              }
            } catch (e) { /* ignore */ }
          }

          if (campusId && campusSelect) {
            const optExists = Array.from(campusSelect.options).some(o => o.value === campusId);
            if (optExists) campusSelect.value = campusId;
            else {
              try {
                await populateCampusDropdown(campusSelectId);
                campusSelect.value = campusId;
              } catch (e) { /* ignore */ }
            }
          }
        }
      } else {
        // offline fallback
        const [indoorRes, roomsRes, infraRes] = await Promise.all([
          fetch("../assets/firestore/IndoorInfrastructure.json").then(r => r.json()),
          fetch("../assets/firestore/Rooms.json").then(r => r.json()),
          fetch("../assets/firestore/Infrastructure.json").then(r => r.json())
        ]);
        const indoorDoc = (indoorRes || []).find(x => x.room_id === roomId) || (roomsRes || []).find(x => x.room_id === roomId);
        if (!indoorDoc) return;

        setNameIfAllowed(indoorDoc.name || indoorDoc.room_name || "");

        let campusId = (roomsRes || []).find(r => r.room_id === roomId)?.campus_id;
        if (!campusId && indoorDoc.infra_id) campusId = (infraRes || []).find(i => i.infra_id === indoorDoc.infra_id)?.campus_id;
        if (campusId && campusSelect) {
          const optExists = Array.from(campusSelect.options).some(o => o.value === campusId);
          if (optExists) campusSelect.value = campusId;
        }
      }
    } catch (err) {
      console.error("Error prefilling from indoor infra selection:", err);
    }
  });
}



handleIndoorInfraSelection({
  selectId: "relatedIndoorInfra",
  nameInputId: "nodeName",
  relatedInfraSelectId: "relatedInfra",
  campusSelectId: "campusDropdown"
});
handleIndoorInfraSelection({
  selectId: "editRelatedIndoorInfra",
  nameInputId: "editNodeName",
  relatedInfraSelectId: "editRelatedInfra",
  campusSelectId: "editCampusDropdown"
});








document.querySelector(".nodetbl").addEventListener("click", async (e) => {
  if (!e.target.classList.contains("fa-edit")) return;

  const row = e.target.closest("tr");
  if (!row) return;

  const nodeId = row.querySelector("td")?.textContent?.trim();
  if (!nodeId) return;

  try {
    
    const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
    let nodeData = null;
    let versionRef = null;

    for (const mapDoc of mapVersionsSnap.docs) {
      const mapData = mapDoc.data();
      const currentVersion = mapData.current_version;
      if (!currentVersion) continue;

      const versionDocRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
      const versionSnap = await getDoc(versionDocRef);
      if (!versionSnap.exists()) continue;

      const versionData = versionSnap.data();
      const nodeFound = versionData.nodes?.find((n) => n.node_id === nodeId);
      if (nodeFound) {
        nodeData = nodeFound;
        versionRef = versionDocRef;
        break;
      }
    }

    if (!nodeData) {
      showModal('error', 'Node not found in the current map versions.');
      return;
    }

    
    await populateInfraDropdown("editRelatedInfra");
    document.getElementById("editRelatedInfra").value = nodeData.related_infra_id ?? "";

    await populateIndoorInfraDropdown("editRelatedIndoorInfra", nodeData.related_room_id ?? null);
    document.getElementById("editRelatedIndoorInfra").value = nodeData.related_room_id ?? "";

    await populateCampusDropdown("editCampusDropdown");
    document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

    
    document.getElementById("editNodeId").value = nodeData.node_id ?? "";
    document.getElementById("editNodeIdHidden").value = nodeData.node_id ?? "";
    document.getElementById("editNodeName").value = nodeData.name ?? "";
    document.getElementById("editLatitude").value = nodeData.latitude ?? "";
    document.getElementById("editLongitude").value = nodeData.longitude ?? "";

    
    let typeSelect = document.getElementById("editNodeType");
    let relatedInfraSelect = document.getElementById("editRelatedInfra");
    let relatedIndoorSelect = document.getElementById("editRelatedIndoorInfra");

    // Normalize stored type values to UI values (e.g. 'indoorinfra' -> 'indoorInfra')
    const mapStoredTypeToUI = (t) => {
      if (!t) return "";
      const s = String(t).toLowerCase();
      if (s === "indoor" || s === "indoorinfra" || s === "indoor_infra" ) return "indoorInfra";
      if (s === "infrastructure") return "infrastructure";
      if (s === "barrier") return "barrier";
      if (s === "intermediate") return "intermediate";
      return "";
    };

    let typeValue = mapStoredTypeToUI(nodeData.type);

    
    if (typeSelect.tagName !== "SELECT") {
      const parent = typeSelect.parentNode;
      const newSelect = document.createElement("select");
      newSelect.id = "editNodeType";
      newSelect.innerHTML = `
        <option value="">Select type</option>
        <option value="infrastructure">Infrastructure</option>
        <option value="indoorInfra">Indoor Infrastructure</option>
        <option value="barrier">Barrier</option>
        <option value="intermediate">Intermediate</option>
      `;
      parent.replaceChild(newSelect, typeSelect);
      typeSelect = newSelect;
    }

    
    if (["infrastructure", "indoorInfra", "barrier", "intermediate"].includes(typeValue)) {
      typeSelect.value = typeValue;
    } else {
      typeSelect.value = "";
    }

    

const coordinatesBlock = document.getElementById("coordinatesGroup");
const indoorDetails = document.getElementById("editIndoorDetails");



function updateDropdownStates(selectedType) {
  
  relatedInfraSelect.disabled = false;
  relatedIndoorSelect.disabled = false;

  switch (selectedType) {
    case "infrastructure":
    case "barrier":
      relatedIndoorSelect.disabled = true;
      relatedInfraSelect.disabled = false;
      
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;

    case "intermediate":
      relatedInfraSelect.disabled = true;
      relatedIndoorSelect.disabled = true;
      
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;

    case "indoorInfra":
      relatedInfraSelect.disabled = true;
      relatedIndoorSelect.disabled = false;
      
      if (coordinatesBlock) coordinatesBlock.style.display = "none";
      if (indoorDetails) indoorDetails.style.display = "block";
      break;

    default:
      relatedInfraSelect.disabled = false;
      relatedIndoorSelect.disabled = false;
      
      if (coordinatesBlock) coordinatesBlock.style.display = "block";
      if (indoorDetails) indoorDetails.style.display = "none";
      break;
  }
}


updateDropdownStates(typeSelect.value);


typeSelect.addEventListener("change", (e) => {
  updateDropdownStates(e.target.value);
});


    // Show indoor details if either indoor payload exists or mapped UI type is indoorInfra
    if (nodeData.indoor || mapStoredTypeToUI(nodeData.type) === "indoorInfra") {
      indoorDetails.style.display = "block";
      document.getElementById("editFloor").value = nodeData.indoor?.floor ?? "";
      document.getElementById("editXCoord").value = nodeData.indoor?.x ?? "";
      document.getElementById("editYCoord").value = nodeData.indoor?.y ?? "";
    } else {
      indoorDetails.style.display = "none";
      document.getElementById("editFloor").value = "";
      document.getElementById("editXCoord").value = "";
      document.getElementById("editYCoord").value = "";
    }

    
    document.getElementById("editCampusDropdown").value = nodeData.campus_id ?? "";

    
    document.getElementById("editNodeModal").style.display = "flex";
    document.getElementById("editNodeForm").dataset.mapVersionRef = versionRef.path;
  } catch (err) {
    console.error("Error opening edit modal:", err);
    showModal('error', 'Failed to prepare edit modal. Please try again.');
  }
});








document.getElementById("editNodeForm").addEventListener("submit", async (e) => {
  e.preventDefault();

  const form = e.target;
  const versionRefPath = form.dataset.mapVersionRef; 
  if (!versionRefPath) {
    showModal('error', 'No map version reference found for update.');
    return;
  }

  const nodeId = document.getElementById("editNodeIdHidden").value.trim();
  const nodeName = document.getElementById("editNodeName").value.trim();
  const type = document.getElementById("editNodeType").value;
  // Normalize storage type: UI uses 'indoorInfra' but storage expects 'indoorinfra'
  const storageType = (type === "indoorInfra" || type === "indoor") ? "indoorinfra" : type;
  const relatedInfraId = document.getElementById("editRelatedInfra").value;
  const relatedIndoorInfraId = document.getElementById("editRelatedIndoorInfra").value;
  const campusId = document.getElementById("editCampusDropdown").value;

  
  let latitude = parseFloat(document.getElementById("editLatitude").value);
  let longitude = parseFloat(document.getElementById("editLongitude").value);

  let indoor = null;
    if (type === "indoorInfra") {
    indoor = {
      floor: document.getElementById("editFloor").value.trim(),
      x: parseFloat(document.getElementById("editXCoord").value) || 0,
      y: parseFloat(document.getElementById("editYCoord").value) || 0
    };
    
    latitude = null;
    longitude = null;
  }

  try {
    const versionRef = doc(db, versionRefPath);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) throw new Error("Version document not found!");

    const versionData = versionSnap.data();
        const updatedNodes = versionData.nodes.map((node) => {
      if (node.node_id === nodeId) {
        return {
          ...node,
          name: nodeName,
          latitude,
          longitude,
          // write normalized storage type so downstream logic treats indoor nodes correctly
          type: storageType,
          related_infra_id: relatedInfraId,
          related_room_id: relatedIndoorInfraId,
          indoor,
          campus_id: campusId,
          updated_at: new Date(),
        };
      }
      return node;
    });

    await updateDoc(versionRef, { nodes: updatedNodes });

    
    const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
    await updateDoc(staticDataRef, {
        infrastructure_updated: true,
    });


    showModal('success', 'Node has been updated successfully!');
    document.getElementById("editNodeModal").style.display = "none";
    renderNodesTable();

  } catch (err) {
    console.error("Error updating node:", err);
    showModal('error', 'Failed to update node. Please try again.');
  }
});












































async function generateNextIndoorEdgeId() {
    const indoorEdgesSnap = await getDocs(collection(db, "IndoorEdges"));
    const edges = indoorEdgesSnap.docs.map(doc => doc.data());

    let maxNum = 0;
    edges.forEach(edge => {
        if (edge.indooredge_id) {
            const num = parseInt(edge.indooredge_id.replace("IED-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `IED-${String(maxNum + 1).padStart(3, "0")}`;
    document.querySelector("#addEdgeModal input[type='text']").value = nextId;
    return nextId;
}




async function generateNextEdgeId() {
    
    const mapSelect = document.getElementById("mapSelect");
    const mapId = mapSelect ? mapSelect.value : null;
    if (!mapId) return;

    const mapDocRef = doc(db, "MapVersions", String(mapId));
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) return;

    const currentVersion = mapDocSnap.data().current_version || "v1.0.0";
    const versionRef = doc(db, "MapVersions", String(mapId), "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) return;

    const edges = Array.isArray(versionSnap.data().edges) ? versionSnap.data().edges : [];

    let maxNum = 0;
    edges.forEach(edge => {
        if (edge.edge_id) {
            const num = parseInt(edge.edge_id.replace("EDG-", ""));
            if (!isNaN(num) && num > maxNum) maxNum = num;
        }
    });

    const nextId = `EDG-${String(maxNum + 1).padStart(3, "0")}`;
    document.querySelector("#addEdgeModal input[type='text']").value = nextId;
    return nextId;
}



async function loadNodesDropdownsForEdge() {
    const startNodeSelect = document.getElementById("startNode");
    const endNodeSelect = document.getElementById("endNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML = `<option value="">Select end node</option>`;

    try {
        // Read UI dropdown values to get the selected map/campus/version
        const mapSelect = document.getElementById("mapSelect");
        const campusSelect = document.getElementById("campusSelect");
        const versionSelect = document.getElementById("versionSelect");

        const selectedMapId = mapSelect ? mapSelect.value : null;
        const selectedCampus = campusSelect ? campusSelect.value : null;
        const selectedVersion = versionSelect ? versionSelect.value : null;

        if (!selectedMapId || !selectedCampus || !selectedVersion) {
            console.warn("Cannot load nodes: Missing map/campus/version selection");
            return;
        }

        // Load ONLY the selected map's version
        const versionRef = doc(db, "MapVersions", selectedMapId, "versions", selectedVersion);
        const versionSnap = await getDoc(versionRef);

        if (!versionSnap.exists()) {
            console.warn(`No version data found for: ${selectedMapId} → ${selectedVersion}`);
            return;
        }

        const versionData = versionSnap.data();
        const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];

        // Filter nodes: not deleted, belong to selected campus, and are infrastructure/intermediate types
        const filteredNodes = nodes.filter(n =>
            !n.is_deleted &&
            n.campus_id === selectedCampus &&
            (n.type === "infrastructure" || n.type === "intermediate")
        );

        // Sort by creation date
        filteredNodes.sort((a, b) => {
            if (!a.created_at || !b.created_at) return 0;
            return a.created_at.seconds - b.created_at.seconds;
        });

        // Populate both dropdowns
        filteredNodes.forEach(node => {
            if (node.node_id) {
                const label = `${node.node_id} - ${node.name || "Unnamed"}`;

                const option1 = document.createElement("option");
                option1.value = node.node_id;
                option1.textContent = label;
                startNodeSelect.appendChild(option1);

                const option2 = document.createElement("option");
                option2.value = node.node_id;
                option2.textContent = label;
                endNodeSelect.appendChild(option2);
            }
        });

        console.log(`✅ Loaded ${filteredNodes.length} nodes for ${selectedMapId} - ${selectedCampus} - ${selectedVersion}`);
    } catch (err) {
        console.error("Error loading nodes into edge dropdowns:", err);
    }
}



const roomToggle = document.getElementById("roomEdgeToggle");
const infraEdgeRow = document.getElementById("infraEdgeRow");
const roomEdgeRow = document.getElementById("roomEdgeRow");
const pathElevationRow = document.getElementById("pathElevationRow");

roomToggle.addEventListener("change", async () => {
    if (roomToggle.checked) {
        // Show room dropdowns
        infraEdgeRow.style.display = "none";
        roomEdgeRow.style.display = "block";
        pathElevationRow.style.display = "none";

        await loadInfraWithRooms();
        await generateNextIndoorEdgeId(); // <-- updates the input with the next room edge ID

    } else {
        // Show normal node edge inputs
        infraEdgeRow.style.display = "flex";
        roomEdgeRow.style.display = "none";
        pathElevationRow.style.display = "flex";

        await generateNextEdgeId(); // <-- updates the input for normal node edges
    }
});

// Load infrastructures that have rooms
async function loadInfraWithRooms() {
    const infraSelect = document.getElementById("infraForRooms");
    infraSelect.innerHTML = `<option value="">Select infrastructure</option>`;

    try {
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        const roomsSnap = await getDocs(collection(db, "IndoorInfrastructure"));
        const rooms = [];
        roomsSnap.forEach(doc => {
            const r = doc.data();
            if (!r.is_deleted) rooms.push(r);
        });

        const infraMap = new Map(); // infra_id => node info
        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            const currentCampus = mapData.current_active_campus;
            const currentVersion = mapData.current_version;
            if (!currentCampus || !currentVersion) continue;

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const nodes = versionSnap.data().nodes || [];
            nodes.forEach(n => {
                if (!n.is_deleted && n.campus_id === currentCampus && n.related_infra_id && !n.related_room_id) {
                    const hasRooms = rooms.some(r => r.infra_id === n.related_infra_id);
                    if (hasRooms) infraMap.set(n.related_infra_id, n);
                }
            });
        }

        infraMap.forEach((node, infraId) => {
            const option = document.createElement("option");
            option.value = infraId;
            option.textContent = `${node.node_id} - ${node.name || "Unnamed"}`;
            infraSelect.appendChild(option);
        });

    } catch (err) {
        console.error("Failed to load infrastructures with rooms:", err);
    }
}

// Load rooms when infra is selected
document.getElementById("infraForRooms").addEventListener("change", async (e) => {
    const infraId = e.target.value;
    const startRoomSelect = document.getElementById("startRoom");
    const endRoomSelect = document.getElementById("endRoom");
    startRoomSelect.innerHTML = `<option value="">Select start room</option>`;
    endRoomSelect.innerHTML = `<option value="">Select end room</option>`;

    if (!infraId) return;

    try {
        const roomsSnap = await getDocs(collection(db, "IndoorInfrastructure"));
        roomsSnap.forEach(doc => {
            const r = doc.data();
            if (!r.is_deleted && r.infra_id === infraId) {
                const option1 = document.createElement("option");
                option1.value = r.room_id;
                option1.textContent = `${r.room_id} - ${r.name}`;
                startRoomSelect.appendChild(option1);

                const option2 = document.createElement("option");
                option2.value = r.room_id;
                option2.textContent = `${r.room_id} - ${r.name}`;
                endRoomSelect.appendChild(option2);
            }
        });
    } catch (err) {
        console.error("Failed to load rooms:", err);
    }
});










let pendingEdgeData = null; 


document.querySelector("#addEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const roomToggle = document.getElementById("roomEdgeToggle");

    if (roomToggle.checked) {
        const infraId = document.getElementById("infraForRooms").value;
        const fromRoom = document.getElementById("startRoom").value;
        const toRoom = document.getElementById("endRoom").value;

        if (!infraId || !fromRoom || !toRoom) {
            showModal('error', 'Please select infrastructure and both rooms.');
            return;
        }

        try {
            // Generate sequential Indoor Edge ID
            const indoorEdgesSnap = await getDocs(collection(db, "IndoorEdges"));
            const count = indoorEdgesSnap.size + 1;
            const indooredgeId = `IED-${String(count).padStart(3, '0')}`;

            const indoorEdgeData = {
                indooredge_id: indooredgeId,
                infra_id: infraId,
                from_indoor: fromRoom,
                to_indoor: toRoom,
                is_deleted: false,
                is_active: true,
                created_at: new Date()
            };

            await addDoc(collection(db, "IndoorEdges"), indoorEdgeData);
            showModal('success', `Indoor edge ${indooredgeId} added successfully!`);
            document.getElementById("addEdgeModal").style.display = "none";
            pendingEdgeData = null;

        } catch (err) {
            console.error(err);
            showModal('error', 'Failed to add indoor edge.');
        }

        return; // exit early so node edge logic doesn't run
    }


    // ----- NODE EDGE LOGIC (existing) -----
    const edgeId = document.querySelector("#addEdgeModal input[type='text']").value;
    const startNode = document.getElementById("startNode").value;
    const endNode = document.getElementById("endNode").value;

    let pathTypeEl = document.getElementById("pathType") || document.querySelector("input[name='pathType']");
    let pathType = pathTypeEl ? pathTypeEl.value.trim() : "";

    let elevationEl = document.getElementById("elevation") || document.querySelector("input[name='elevation']");
    let elevation = elevationEl ? elevationEl.value.trim() : "";

    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");
    if (pathType && !["via_overpass", "via_underpass", "stairs", "ramp"].includes(pathType)) pathType = toSnakeCase(pathType);
    if (elevation && !["slope_up", "slope_down", "flat"].includes(elevation)) elevation = toSnakeCase(elevation);

    pendingEdgeData = {
        edge_id: edgeId,
        from_node: startNode,
        to_node: endNode,
        distance: null,
        path_type: pathType || null,
        elevations: elevation || null,
        is_active: true,
        is_deleted: false,
        created_at: new Date()
    };

    document.getElementById("edgeSaveModal").style.display = "flex";
});



document.getElementById("closeEdgeSaveModal").addEventListener("click", () => {
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});

document.getElementById("overwriteEdgeBtn").addEventListener("click", async () => {
    await saveEdge("overwrite");
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});

document.getElementById("newVersionEdgeBtn").addEventListener("click", async () => {
    await saveEdge("newVersion");
    pendingEdgeData = null;
    document.getElementById("edgeSaveModal").style.display = "none";
});


function haversineDistance(lat1, lon1, lat2, lon2) {
    const R = 6371000; 
    const toRad = (deg) => deg * Math.PI / 180;

    const dLat = toRad(lat2 - lat1);
    const dLon = toRad(lon2 - lon1);

    const a =
        Math.sin(dLat / 2) * Math.sin(dLat / 2) +
        Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) *
        Math.sin(dLon / 2) * Math.sin(dLon / 2);

    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c; 
}

async function saveEdge(option) {
    if (!pendingEdgeData) return;

    const roomToggle = document.getElementById("roomEdgeToggle");

    // ----------------- ROOM EDGE LOGIC -----------------
    if (roomToggle.checked) {
        const { infra_id, from_indoor, to_indoor } = pendingEdgeData;

        if (!infra_id || !from_indoor || !to_indoor) {
            showModal('error', 'Missing infrastructure or room info.');
            return;
        }

        try {
            await addDoc(collection(db, "IndoorEdges"), {
                indooredge_id: `IED-${Date.now()}`,
                infra_id: infra_id,
                from_indoor: from_indoor,
                to_indoor: to_indoor,
                is_active: true,
                is_deleted: false,
                created_at: new Date()
            });

            showModal('success', 'Indoor edge added successfully!');
            document.getElementById("edgeSaveModal").style.display = "none";
            document.getElementById("addEdgeModal").style.display = "none";
            pendingEdgeData = null;
        } catch (err) {
            console.error(err);
            showModal('error', 'Failed to save indoor edge.');
        }

        return; // Exit early so normal node edge logic doesn't run
    }

    // ----------------- NODE EDGE LOGIC (existing) -----------------
    try {
        // Read UI dropdown values to get the selected map and version
        const mapSelect = document.getElementById("mapSelect");
        const versionSelect = document.getElementById("versionSelect");

        const selectedMapId = mapSelect ? mapSelect.value : null;
        const selectedVersion = versionSelect ? versionSelect.value : null;

        if (!selectedMapId || !selectedVersion) {
            showModal('error', 'Please select a map and version before adding an edge.');
            return;
        }

        const mapDocId = selectedMapId;
        const mapDocRef = doc(db, "MapVersions", mapDocId);
        const mapDocSnap = await getDoc(mapDocRef);

        if (!mapDocSnap.exists()) {
            showModal('error', 'Selected map not found. Please try again.');
            return;
        }

        const mapData = mapDocSnap.data();
        const mapId = mapDocId;
        const currentVersion = selectedVersion;

        const versionRef = doc(db, "MapVersions", mapDocId, "versions", currentVersion);
        const versionSnap = await getDoc(versionRef);

        let oldNodes = [];
        let oldEdges = [];

        if (versionSnap.exists()) {
            const versionData = versionSnap.data();
            oldNodes = versionData.nodes || [];
            oldEdges = versionData.edges || [];
        }

        const startNode = oldNodes.find(n => n.node_id === pendingEdgeData.from_node);
        const endNode = oldNodes.find(n => n.node_id === pendingEdgeData.to_node);

        if (!startNode || !endNode) {
            showModal('error', 'Start or End node not found in MapVersion.');
            return;
        }

        const distance = haversineDistance(
            startNode.latitude, startNode.longitude,
            endNode.latitude, endNode.longitude
        );

        pendingEdgeData.distance = Number(distance.toFixed(2));

        if (option === "overwrite") {
            await updateDoc(versionRef, {
                edges: arrayUnion(pendingEdgeData)
            });
        } else if (option === "newVersion") {
            let versionMatch = /^v(\d+)\.(\d+)\.(\d+)$/.exec(currentVersion);
            let major = 1, minor = 0, patch = 0;

            if (versionMatch) {
                major = parseInt(versionMatch[1], 10);
                minor = parseInt(versionMatch[2], 10);
                patch = parseInt(versionMatch[3], 10);
            }

            if (patch < 99) patch += 1;
            else { patch = 0; minor += 1; }

            const newVersion = `v${major}.${minor}.${patch}`;
            const migratedNodes = oldNodes.map(n => ({ ...n }));
            const migratedEdges = oldEdges.map(e => ({ ...e }));
            migratedEdges.push({ ...pendingEdgeData });

            await setDoc(doc(db, "MapVersions", mapDocId, "versions", newVersion), {
                nodes: migratedNodes,
                edges: migratedEdges
            });

            await updateDoc(doc(db, "MapVersions", mapDocId), { current_version: newVersion });
            showModal('success', `New version created: ${newVersion} with migrated nodes and edges`);
        }

        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, { infrastructure_updated: true });

        renderEdgesTable();
        loadMap(mapId);

        document.getElementById("edgeSaveModal").style.display = "none";
        document.getElementById("addEdgeModal").style.display = "none";

        pendingEdgeData = null;

    } catch (err) {
        console.error(err);
        showModal('error', 'Failed to save Edge. Please try again.');
    }
}






async function renderEdgesTable() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;

    
    tbody.innerHTML = `
        <tr>
            <td colspan="6" style="text-align:center;">
                <div class="spinner"></div>
                <span class="loading-text">Loading edges...</span>
            </td>
        </tr>
    `;

    try {
        // Get the currently selected map and campus from UI
        const mapSelect = document.getElementById("mapSelect");
        const campusSelect = document.getElementById("campusSelect");
        const versionSelect = document.getElementById("versionSelect");
        
        const selectedMapId = mapSelect ? mapSelect.value : null;
        const selectedCampus = campusSelect ? campusSelect.value : null;
        const selectedVersion = versionSelect ? versionSelect.value : null;
        
        if (!selectedMapId || !selectedCampus || !selectedVersion) {
            tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; color:orange;">Please select a map, campus, and version.</td></tr>`;
            return;
        }

        let edges = [];

        if (navigator.onLine) {
            // Load ONLY the selected map version's data
            const versionRef = doc(db, "MapVersions", selectedMapId, "versions", selectedVersion);
            const versionSnap = await getDoc(versionRef);
            
            if (versionSnap.exists()) {
                const versionData = versionSnap.data();
                const allNodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
                const allEdges = Array.isArray(versionData.edges) ? versionData.edges : [];

                const nodeCampusMap = {};
                allNodes.forEach(n => {
                    if (n.node_id && n.campus_id) nodeCampusMap[n.node_id] = n.campus_id;
                });

                // Filter edges to only those where both nodes belong to the selected campus
                const filteredEdges = allEdges.filter(e => {
                    if (e.is_deleted) return false;
                    const fromCampus = nodeCampusMap[e.from_node];
                    const toCampus = nodeCampusMap[e.to_node];
                    return fromCampus === selectedCampus && toCampus === selectedCampus;
                });

                edges = filteredEdges;
            }
        } else {
            // Offline mode: load from static JSON files
            const mapVersionsRes = await fetch("../assets/firestore/MapVersions.json");
            const mapVersions = await mapVersionsRes.json();
            
            const mapData = mapVersions.find(m => m.id === selectedMapId);
            if (mapData) {
                const version = mapData.versions.find(v => v.id === selectedVersion);
                if (version) {
                    const allNodes = Array.isArray(version.nodes) ? version.nodes : [];
                    const allEdges = Array.isArray(version.edges) ? version.edges : [];

                    const nodeCampusMap = {};
                    allNodes.forEach(n => {
                        if (n.node_id && n.campus_id) nodeCampusMap[n.node_id] = n.campus_id;
                    });

                    // Filter edges to only those where both nodes belong to the selected campus
                    const filteredEdges = allEdges.filter(e => {
                        if (e.is_deleted) return false;
                        const fromCampus = nodeCampusMap[e.from_node];
                        const toCampus = nodeCampusMap[e.to_node];
                        return fromCampus === selectedCampus && toCampus === selectedCampus;
                    });

                    edges = filteredEdges;
                }
            }
        }

        
        edges.sort((a, b) => (a?.created_at?.seconds || 0) - (b?.created_at?.seconds || 0));

        
        tbody.innerHTML = "";

        
        edges.forEach(data => {
            const formatText = (value) => {
                if (!value) return "-";
                return value.toString().split("_").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ");
            };

            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${data.edge_id}</td>
                <td>${data.from_node} → ${data.to_node}</td>
                <td>${data.distance || "-"}</td>
                <td>${formatText(data.path_type)}</td>
                <td>${formatText(data.elevations)}</td>
                <td class="actions">
                    <i class="fas fa-edit"></i>
                    <i class="fas fa-trash" data-id="${data.edge_id}"></i>
                </td>
            `;
            tbody.appendChild(tr);
        });

        setupEdgeDeleteHandlers();
        console.log(`✅ Rendered ${edges.length} edges for the current active campus`);

    } catch (err) {
        console.error("Error loading edges:", err);
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:red;">Failed to load edges</td></tr>`;
    }
}







































































window.openEdgeModal = async function () {
    document.getElementById("addEdgeModal").style.display = "flex";
    await generateNextEdgeId();
    await loadNodesDropdownsForEdge();
};
window.closeEdgeModal = function () {
    document.getElementById("addEdgeModal").style.display = "none";
};








const selectTemplates = {};
document.addEventListener("DOMContentLoaded", () => {
    ["editPathType", "editElevation"].forEach(id => {
        const el = document.getElementById(id);
        if (el) selectTemplates[id] = el.outerHTML;
    });
});


function ensureSelectExists(selectId) {
    let select = document.getElementById(selectId);
    if (select) return select;

    const modal = document.getElementById("editEdgeModal");
    const input = modal.querySelector(`input[name='${selectId}']`);
    if (input) {
        const tpl = selectTemplates[selectId];
        if (!tpl) return null;
        const wrapper = document.createElement("div");
        wrapper.innerHTML = tpl.trim();
        const newSelect = wrapper.firstElementChild;
        input.parentNode.replaceChild(newSelect, input);
        return newSelect;
    }
    return null;
}


function handlePreselectOrCustom(selectId, value) {
    let select = document.getElementById(selectId);
    if (!select) select = ensureSelectExists(selectId);

    if (!select) return;

    if (!value) {
        select.value = "";
        return;
    }

    const optionExists = Array.from(select.options).some(opt => opt.value === value);
    if (optionExists) {
        select.value = value;
    } else {
        
        const input = document.createElement("input");
        input.type = "text";
        input.name = selectId;
        input.value = value;
        input.classList.add("custom-input");
        select.parentNode.replaceChild(input, select);
    }
}


document.querySelector(".edgetbl").addEventListener("click", async (e) => {
    if (!e.target.classList.contains("fa-edit")) return;

    const tr = e.target.closest("tr");
    const edgeId = tr.children[0].textContent.trim();
    if (!edgeId) return;

    try {
        let edgeData = null;

        
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            const currentVersion = mapData.current_version;
            if (!currentVersion) continue;

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();
            const found = Array.isArray(versionData.edges)
                ? versionData.edges.find(e => e.edge_id === edgeId)
                : null;

            if (found) {
                edgeData = found;
                break;
            }
        }

        if (!edgeData) {
            showModal('error', 'Edge data not found in MapVersions.');
            return;
        }

        
        document.getElementById("editEdgeId").value = edgeData.edge_id || "";

        await loadNodesDropdownsForEditEdge(edgeData.from_node, edgeData.to_node);

        handlePreselectOrCustom("editPathType", edgeData.path_type);
        handlePreselectOrCustom("editElevation", edgeData.elevations || edgeData.elevation);

        
        const modal = document.getElementById("editEdgeModal");
        modal.dataset.edgeId = edgeData.edge_id;
        modal.dataset.mapId = edgeData.map_id || "";
        modal.style.display = "flex";

    } catch (err) {
        console.error("Error opening edge edit modal:", err);
        showModal('error', 'Failed to load edge data. See console for details.');
    }
});




async function loadNodesDropdownsForEditEdge(selectedFrom, selectedTo) {
    const startNodeSelect = document.getElementById("editStartNode");
    const endNodeSelect = document.getElementById("editEndNode");

    startNodeSelect.innerHTML = `<option value="">Select start node</option>`;
    endNodeSelect.innerHTML = `<option value="">Select end node</option>`;

    try {
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();

            
            const currentMap = mapData.current_active_map;
            const currentCampus = mapData.current_active_campus;
            const currentVersion = mapData.current_version;

            if (!currentMap || !currentCampus || !currentVersion) {
                
                continue;
            }

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();
            const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];

            
            const filteredNodes = nodes.filter(n => !n.is_deleted && n.campus_id === currentCampus);

            
            filteredNodes.sort((a, b) => {
                const aSec = (a.created_at && a.created_at.seconds) ? a.created_at.seconds : 0;
                const bSec = (b.created_at && b.created_at.seconds) ? b.created_at.seconds : 0;
                return aSec - bSec;
            });

            
            filteredNodes.forEach(node => {
                if (!node.node_id) return;
                const label = `${node.node_id} - ${node.name || "Unnamed"}`;

                const opt1 = document.createElement("option");
                opt1.value = node.node_id;
                opt1.textContent = label;
                if (node.node_id === selectedFrom) opt1.selected = true;
                startNodeSelect.appendChild(opt1);

                const opt2 = document.createElement("option");
                opt2.value = node.node_id;
                opt2.textContent = label;
                if (node.node_id === selectedTo) opt2.selected = true;
                endNodeSelect.appendChild(opt2);
            });
        }
    } catch (err) {
        console.error("Error loading nodes into edit edge dropdowns:", err);
    }
}



function handleOtherOption(selectId) {
    const select = document.getElementById(selectId);
    if (!select) return;

    select.addEventListener("change", function () {
        if (this.value === "other") {
            const input = document.createElement("input");
            input.type = "text";
            input.name = selectId;
            input.placeholder = "Enter your own value";
            input.classList.add("custom-input");

            this.parentNode.replaceChild(input, this);

            input.addEventListener("blur", function () {
                if (input.value.trim() === "") {
                    input.parentNode.replaceChild(select, input);
                    select.value = "";
                }
            });
        }
    });
}
handleOtherOption("pathType");
handleOtherOption("elevation");
handleOtherOption("editPathType");
handleOtherOption("editElevation");



document.querySelector("#editEdgeModal form").addEventListener("submit", async (e) => {
    e.preventDefault();

    const modal = document.getElementById("editEdgeModal");
    const docId = modal.dataset.docId; 

    
    const getFieldValue = (id) => {
        const select = document.getElementById(id);
        const input = document.querySelector(`input[name='${id}']`);
        return select ? select.value.trim() : (input ? input.value.trim() : "");
    };

    
    const toSnakeCase = str => str.toLowerCase().replace(/\s+/g, "_");

    let pathType = getFieldValue("editPathType");
    let elevation = getFieldValue("editElevation");

    if (pathType && !["via_overpass", "via_underpass", "stairs", "ramp"].includes(pathType)) {
        pathType = toSnakeCase(pathType);
    }
    if (elevation && !["slope_up", "slope_down", "flat"].includes(elevation)) {
        elevation = toSnakeCase(elevation);
    }

    const updatedData = {
        from_node: document.getElementById("editStartNode").value,
        to_node: document.getElementById("editEndNode").value,
        path_type: pathType || null,
        elevations: elevation || null,
    };

    try {
        
        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        let activeVersionRef = null;

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            if (mapData.current_active_map && mapData.current_active_campus && mapData.current_version) {
                activeVersionRef = doc(db, "MapVersions", mapDoc.id, "versions", mapData.current_version);
                break;
            }
        }

        if (!activeVersionRef) {
            showModal('error', 'No active map version found!');
            return;
        }

        
        const versionSnap = await getDoc(activeVersionRef);
        if (!versionSnap.exists()) {
            showModal('error', 'Active version document not found!');
            return;
        }

        const versionData = versionSnap.data();
        const edges = Array.isArray(versionData.edges) ? [...versionData.edges] : [];

        
        const edgeIndex = edges.findIndex(edge => edge.edge_id === docId);
        if (edgeIndex === -1) {
            showModal('error', 'Edge not found in current version!');
            return;
        }

        edges[edgeIndex] = {
            ...edges[edgeIndex],
            ...updatedData,
            updated_at: new Date(),
        };

        
        await updateDoc(activeVersionRef, { edges });

        
        const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
        await updateDoc(staticDataRef, {
            infrastructure_updated: true,
        });

        showModal('success', 'Edge has been updated successfully!');
        modal.style.display = "none";
        renderEdgesTable();

    } catch (err) {
        console.error("Error updating edge:", err);
        showModal('error', 'Failed to update edge. Please try again.');
    }
});


document.getElementById("cancelEditEdgeBtn").addEventListener("click", () => {
    document.getElementById("editEdgeModal").style.display = "none";
});
document.getElementById("editEdgeModal").addEventListener("click", (e) => {
    if (e.target.id === "editEdgeModal") {
        document.getElementById("editEdgeModal").style.display = "none";
    }
});















window.onload = () => {

    renderEdgesTable();
};

document.addEventListener("DOMContentLoaded", function () {
    const tabs = document.querySelectorAll(".top-tabs .tab");
    const tables = document.querySelectorAll(".bottom-tbl > div");
    const addButton = document.querySelector(".addnode .add-btn");
    const breadcrumbDetail = document.querySelector(".breadcrumb .span-details"); 

    
    const addNodeModal = document.getElementById("addNodeModal");
    const addEdgeModal = document.getElementById("addEdgeModal");

    const cancelNodeBtn = document.querySelector("#addNodeModal .cancel-btn");
    const cancelEdgeBtn = document.querySelector("#addEdgeModal .cancel-btn");

    
    const buttonTexts = ["Add Node", "Add Edge"];

    
    const breadcrumbTexts = ["Nodes", "Edges"]; 

    
    tabs.forEach((tab, index) => {
        tab.addEventListener("click", () => {
            tabs.forEach(t => t.classList.remove("active"));
            tables.forEach(tbl => tbl.style.display = "none");
            tab.classList.add("active");
            tables[index].style.display = "block";

            if (buttonTexts[index]) {
                addButton.textContent = buttonTexts[index];
            }

            
            if (breadcrumbTexts[index]) {
                breadcrumbDetail.textContent = ' ' + breadcrumbTexts[index];
            }
        });
    });

    
    addButton.addEventListener("click", () => {
        if (addButton.textContent === "Add Node") {
            window.openNodeModal();
            addNodeModal.style.display = "flex";
        } else if (addButton.textContent === "Add Edge") {
            window.openEdgeModal();
            addEdgeModal.style.display = "flex";
        }
    });

    
    cancelNodeBtn.addEventListener("click", () => {
        window.closeNodeModal();
    });
    addNodeModal.addEventListener("click", (e) => {
        if (e.target === addNodeModal) {
            window.closeNodeModal();
        }
    });

    
    cancelEdgeBtn.addEventListener("click", () => {
        addEdgeModal.style.display = "none";
    });
    addEdgeModal.addEventListener("click", (e) => {
        if (e.target === addEdgeModal) {
            addEdgeModal.style.display = "none";
        }
    });
});




document.addEventListener("DOMContentLoaded", function() {
    const indoorCheckbox = document.getElementById("indoorCheckbox");
    const outdoorCheckbox = document.getElementById("outdoorCheckbox");
    const indoorDetails = document.getElementById("indoorDetails");

    if (indoorCheckbox && outdoorCheckbox && indoorDetails) {
        indoorCheckbox.addEventListener("change", function() {
            if (indoorCheckbox.checked) {
                indoorDetails.style.display = "block";
                outdoorCheckbox.checked = false;
            } else {
                indoorDetails.style.display = "none";
            }
        });

        outdoorCheckbox.addEventListener("change", function() {
            if (outdoorCheckbox.checked) {
                indoorCheckbox.checked = false;
                indoorDetails.style.display = "none";
            }
        });
    }
});



  const editNodeModal = document.getElementById("editNodeModal");
  const cancelEditBtn = document.getElementById("cancelEditNodeBtn");

  
  document.querySelector(".nodetbl").addEventListener("click", (e) => {
    if (e.target.classList.contains("fa-edit")) {
      editNodeModal.style.display = "flex"; 
    }
  });

  
  cancelEditBtn.addEventListener("click", () => {
    editNodeModal.style.display = "none";
  });

  
  editNodeModal.addEventListener("click", (e) => {
    if (e.target === editNodeModal) {
      editNodeModal.style.display = "none";
    }
  });


    const editEdgeModal = document.getElementById("editEdgeModal");
  const cancelEditEdgeBtn = document.getElementById("cancelEditEdgeBtn");

  
  document.querySelector(".edgetbl").addEventListener("click", (e) => {
    if (e.target.classList.contains("fa-edit")) {
      editEdgeModal.style.display = "flex"; 
    }
  });

  
  cancelEditEdgeBtn.addEventListener("click", () => {
    editEdgeModal.style.display = "none";
  });

  
  editEdgeModal.addEventListener("click", (e) => {
    if (e.target === editEdgeModal) {
      editEdgeModal.style.display = "none";
    }
  });


  








let edgeToDelete = null;


function setupEdgeDeleteHandlers() {
    const tbody = document.querySelector(".edgetbl tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".fa-trash").forEach(btn => {
        btn.addEventListener("click", () => {
            const tr = btn.closest("tr");
            const edgeId = tr.children[0]?.textContent || "";
            const docId = btn.dataset.id;

            edgeToDelete = { docId, edgeId };
            document.getElementById("deleteEdgePrompt").textContent =
                `Are you sure you want to delete edge "${edgeId}"?`;
            document.getElementById("deleteEdgeModal").style.display = "flex";
        });
    });
}


document.getElementById("confirmDeleteEdgeBtn").addEventListener("click", async () => {
    if (!edgeToDelete) return;
    try {
        await updateDoc(doc(db, "Edges", edgeToDelete.docId), {
            is_deleted: true,
            deletedAt: new Date()
        });
        document.getElementById("deleteEdgeModal").style.display = "none";
        edgeToDelete = null;
        renderEdgesTable();
        showModal('success', 'Edge deleted successfully!');
    } catch (err) {
        showModal('error', 'Failed to delete edge. Please try again.');
    }
});


document.getElementById("cancelDeleteEdgeBtn").addEventListener("click", () => {
    document.getElementById("deleteEdgeModal").style.display = "none";
    edgeToDelete = null;
});


document.getElementById("deleteEdgeModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteEdgeModal")) {
        document.getElementById("deleteEdgeModal").style.display = "none";
        edgeToDelete = null;
    }
});




let nodeToDelete = null;


function setupNodeDeleteHandlers() {
    const tbody = document.querySelector(".nodetbl tbody");
    if (!tbody) return;

    tbody.querySelectorAll(".fa-trash").forEach(btn => {
        btn.addEventListener("click", () => {
            const tr = btn.closest("tr");
      const rowNodeId = tr.children[0]?.textContent?.trim() || "";
      const docIdAttr = btn.dataset.id || null; // data-id (document id) if present
      const dataNodeIdAttr = btn.dataset.nodeId || null; // data-node-id attribute (node_id)

      // Prefer explicit data-id (document id). If missing, fall back to data-node-id or table text.
      const nodeId = dataNodeIdAttr || rowNodeId;

      nodeToDelete = { docId: docIdAttr, nodeId };
            document.getElementById("deleteNodePrompt").textContent =
                `Are you sure you want to delete node "${nodeId}"?`;
            document.getElementById("deleteNodeModal").style.display = "flex";
        });
    });
}


document.getElementById("confirmDeleteNodeBtn").addEventListener("click", async () => {
    if (!nodeToDelete) return;

    try {
        // Nodes are stored in MapVersions/{mapId}/versions/{versionId}/nodes[] array
        // Find the MapVersion document containing this node and remove it from the array
        const nodeIdToDelete = nodeToDelete.nodeId;
        if (!nodeIdToDelete) {
            showModal('error', 'No node selected for deletion.');
            return;
        }

        const mapVersionsSnap = await getDocs(collection(db, "MapVersions"));
        let found = false;

        for (const mapDoc of mapVersionsSnap.docs) {
            const mapData = mapDoc.data();
            const currentVersion = mapData.current_version;
            if (!currentVersion) continue;

            const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
            const versionSnap = await getDoc(versionRef);
            if (!versionSnap.exists()) continue;

            const versionData = versionSnap.data();
            const nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
            const nodeIndex = nodes.findIndex(n => n.node_id === nodeIdToDelete);

            if (nodeIndex !== -1) {
                // Remove node from array
                const updatedNodes = nodes.filter((_, i) => i !== nodeIndex);
                await updateDoc(versionRef, { nodes: updatedNodes });
                found = true;

                // Update infrastructure_updated flag
                const staticDataRef = doc(db, "StaticDataVersions", "GlobalInfo");
                await updateDoc(staticDataRef, { infrastructure_updated: true });
                break;
            }
        }

        if (!found) {
            showModal('error', 'Node not found for deletion.');
            return;
        }

        document.getElementById("deleteNodeModal").style.display = "none";
        nodeToDelete = null;

        renderNodesTable();
        showModal('success', 'Node deleted permanently!');
    } catch (err) {
        console.error(err);
        showModal('error', 'Failed to delete node. Please try again.');
    }
});document.getElementById("cancelDeleteNodeBtn").addEventListener("click", () => {
    document.getElementById("deleteNodeModal").style.display = "none";
    nodeToDelete = null;
});


document.getElementById("deleteNodeModal").addEventListener("click", (e) => {
    if (e.target === document.getElementById("deleteNodeModal")) {
        document.getElementById("deleteNodeModal").style.display = "none";
        nodeToDelete = null;
    }
});


















async function populateMaps() {
    const mapSelect = document.getElementById("mapSelect");
    const campusSelect = document.getElementById("campusSelect");
    const versionSelect = document.getElementById("versionSelect");

    mapSelect.innerHTML = '<option value="">Select Map</option>';
    campusSelect.innerHTML = '<option value="">Select Campus</option>';
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    try {
        let mapsData = [];

        if (navigator.onLine) {
            
            const mapsSnap = await getDocs(collection(db, "MapVersions"));
            mapsData = mapsSnap.docs.map(doc => ({ id: doc.id, ...doc.data() }));
        } else {
            
            const res = await fetch("../assets/firestore/MapVersions.json");
            mapsData = await res.json();
        }

        
        mapsData.forEach(map => {
            const option = document.createElement("option");
            option.value = map.id;
            option.textContent = `${map.map_name || map.id} (${map.id})`;
            mapSelect.appendChild(option);
        });

        
        if (mapsData.length > 0) {
            const firstMapId = mapsData[0].id;
            mapSelect.value = firstMapId;
            await populateCampuses(firstMapId, true, mapsData);
            await populateVersions(firstMapId, true, mapsData);

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(firstMapId);
        }

        
        mapSelect.onchange = null;
        mapSelect.addEventListener("change", async () => {
            const selectedMapId = mapSelect.value;
            if (!selectedMapId) return;

            
            if (navigator.onLine) {
                const mapDocRef = doc(db, "MapVersions", selectedMapId);
                await updateDoc(mapDocRef, { current_active_map: selectedMapId });
            }

            await populateCampuses(selectedMapId, true, mapsData);
            await populateVersions(selectedMapId, true, mapsData);

            await renderNodesTable();
            await renderEdgesTable();
            await loadMap(selectedMapId);
        });

    } catch (err) {
        console.error("Error loading maps:", err);
    }
}

async function populateCampuses(mapId, selectCurrent = true, mapsData = null) {
    const campusSelect = document.getElementById("campusSelect");
    campusSelect.innerHTML = '<option value="">Select Campus</option>';

    let mapData;

    if (navigator.onLine) {
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;
        mapData = mapDocSnap.data();
    } else {
        if (!mapsData) {
            mapsData = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        }
        mapData = mapsData.find(m => m.id === mapId);
        if (!mapData) return;
    }

    const campuses = mapData.campus_included || [];
    const currentCampus = mapData.current_active_campus || "";

    campuses.forEach(campusId => {
        const option = document.createElement("option");
        option.value = campusId;
        option.textContent = campusId;
        campusSelect.appendChild(option);
    });

    if (selectCurrent && currentCampus && campuses.includes(currentCampus)) {
        campusSelect.value = currentCampus;
    } else if (campuses.length > 0) {
        campusSelect.value = campuses[0];
    }

    campusSelect.onchange = null;
    campusSelect.addEventListener("change", async () => {
        const selectedCampus = campusSelect.value;
        const selectedMapId = mapSelect.value;
        const selectedVersion = versionSelect.value;
        if (!selectedCampus) return;

        
        if (navigator.onLine) {
            const mapDocRef = doc(db, "MapVersions", selectedMapId);
            await updateDoc(mapDocRef, { current_active_campus: selectedCampus });
        }

        await renderNodesTable();
        await renderEdgesTable();
        await loadMap(selectedMapId, selectedCampus, selectedVersion);
    });
}

async function populateVersions(mapId, selectCurrent = true, mapsData = null) {
    const versionSelect = document.getElementById("versionSelect");
    versionSelect.innerHTML = '<option value="">Select Version</option>';

    let mapData, currentVersion, versions = [];

    if (navigator.onLine) {
        const mapDocRef = doc(db, "MapVersions", mapId);
        const mapDocSnap = await getDoc(mapDocRef);
        if (!mapDocSnap.exists()) return;

        mapData = mapDocSnap.data();
        currentVersion = mapData.current_version || "";

        const versionsSnap = await getDocs(collection(db, "MapVersions", mapId, "versions"));
        versions = versionsSnap.docs.map(docSnap => ({ id: docSnap.id, ...docSnap.data() }));

    } else {
        if (!mapsData) {
            mapsData = await fetch("../assets/firestore/MapVersions.json").then(res => res.json());
        }
        mapData = mapsData.find(m => m.id === mapId);
        if (!mapData) return;

        currentVersion = mapData.current_version || "";
        versions = mapData.versions || [];
    }

    versions.forEach(v => {
        const option = document.createElement("option");
        option.value = v.id;
        option.textContent = v.id + (v.id === currentVersion ? "  🟢" : "");
        versionSelect.appendChild(option);
    });

    if (selectCurrent && currentVersion) {
        versionSelect.value = currentVersion;
    } else if (versions.length > 0) {
        versionSelect.value = versions[0].id;
    }

    versionSelect.onchange = null;
    versionSelect.addEventListener("change", async () => {
        const selectedVersion = versionSelect.value;
        const selectedMapId = mapSelect.value;
        const selectedCampus = campusSelect.value;
        if (!selectedVersion) return;

        
        if (navigator.onLine) {
            const mapDocRef = doc(db, "MapVersions", selectedMapId);
            await updateDoc(mapDocRef, { current_version: selectedVersion });

            
            const mapVersionsCollection = collection(db, "MapVersions");
            const snapshot = await getDocs(mapVersionsCollection);
            const batch = writeBatch(db);

            snapshot.docs.forEach((docSnap) => {
                batch.update(docSnap.ref, {
                    current_version_updated: true,
                });
            });

            await batch.commit();
            console.log("✅ All MapVersions documents updated: current_version_update = true");
        }

        await renderNodesTable();
        await renderEdgesTable();
        await loadMap(selectedMapId, selectedCampus, selectedVersion);
    });
}




async function setActiveMap(mapId) {
    const mapDocRef = doc(db, "MapVersions", String(mapId)); 
    try {
        await updateDoc(mapDocRef, { current_active_map: String(mapId) });
        console.log(`Current active map updated to ${mapId}`);
    } catch (err) {
        console.error("Error updating current active map:", err);
    }
}


document.addEventListener("DOMContentLoaded", () => {
    populateMaps();
});










function getGeographicCenter(nodes, campusId) {
  
  if (campusId === "CAMP-02") {
    return [6.9130, 122.0630];
  }

  if (!nodes.length) return [6.9130, 122.0630]; 

  let x = 0, y = 0, z = 0;

  nodes.forEach(n => {
    if (!n.latitude || !n.longitude) return;
    const latRad = parseFloat(n.latitude) * Math.PI / 180;
    const lonRad = parseFloat(n.longitude) * Math.PI / 180;

    x += Math.cos(latRad) * Math.cos(lonRad);
    y += Math.cos(latRad) * Math.sin(lonRad);
    z += Math.sin(latRad);
  });

  const total = nodes.length;
  x /= total;
  y /= total;
  z /= total;

  const lon = Math.atan2(y, x);
  const hyp = Math.sqrt(x * x + y * y);
  const lat = Math.atan2(z, hyp);

  return [lat * 180 / Math.PI, lon * 180 / Math.PI];
}



function getCampusBounds(nodes, campusId) {
  
  if (campusId === "CAMP-02") {
    return null; 
  }

  const latLngs = nodes
    .filter(n => n.latitude && n.longitude)
    .map(n => [parseFloat(n.latitude), parseFloat(n.longitude)]);

  return latLngs.length ? L.latLngBounds(latLngs) : null;
}









let mapFull = null;
let mapOverview = null;
let showAllCampuses = false; 
let currentMapId = null;     


document.getElementById("customToggle").addEventListener("change", (e) => {
  showAllCampuses = e.target.checked;
  console.log(showAllCampuses ? "🟢 Showing ALL campuses" : "🔴 Showing active campus only");

  if (currentMapId) {
    loadMap(currentMapId); 
  }
});




async function loadMap(mapId, campusId = null, versionId = null) {
  
  try {
    const container = document.getElementById("map-overview");
    if (container) {
      container.style.position = container.style.position || "relative";
      
      container.querySelectorAll(".map-loader").forEach(n => n.remove());
      const earlyOverlay = document.createElement("div");
      earlyOverlay.className = "map-loader";
      earlyOverlay.innerHTML = `
        <div class="map-loader-inner">
          <div class="map-loader-spinner" aria-hidden="true"></div>
          <div class="map-loader-text">Loading Nodes...</div>
        </div>
      `;
      container.appendChild(earlyOverlay);
      
      await new Promise(res => requestAnimationFrame(() => requestAnimationFrame(res)));
    }
  } catch (e) {
    console.warn("Could not show early map loader:", e);
  }

  try {
    currentMapId = mapId; 
    const safeMapId = String(mapId);

    // Read UI select values if not provided as params
    const mapSelect = document.getElementById("mapSelect");
    const campusSelect = document.getElementById("campusSelect");
    const versionSelect = document.getElementById("versionSelect");
    
    const selectedMapId = mapSelect ? mapSelect.value : safeMapId;
    const selectedCampus = campusSelect ? campusSelect.value : campusId;
    const selectedVersion = versionSelect ? versionSelect.value : versionId;

    let mapData, activeCampus, activeVersion, nodes = [], edges = [];
    let infraMap = {}, roomMap = {}, campusMap = {};

    if (navigator.onLine) {
      
      const mapDocRef = doc(db, "MapVersions", safeMapId);
      const mapDocSnap = await getDoc(mapDocRef);
      if (!mapDocSnap.exists()) {
        
        try { const el = document.getElementById("map-overview")?.querySelector(".map-loader"); if (el) el.remove(); } catch(e){}
        return console.error("❌ Map not found:", safeMapId);
      }

      mapData = mapDocSnap.data();
      // Prefer selectedCampus from UI, then param, then stored value
      activeCampus = selectedCampus || campusId || mapData.current_active_campus;
      // Prefer selectedVersion from UI, then param, then stored value
      activeVersion = String(selectedVersion || versionId || mapData.current_version || "");

      const versionDocRef = doc(db, "MapVersions", safeMapId, "versions", activeVersion);
      const versionDocSnap = await getDoc(versionDocRef);
      if (!versionDocSnap.exists()) {
        try { const el = document.getElementById("map-overview")?.querySelector(".map-loader"); if (el) el.remove(); } catch(e){}
        return console.error("❌ Version not found:", activeVersion);
      }

      const versionData = versionDocSnap.data();
      nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
      edges = Array.isArray(versionData.edges) ? versionData.edges : [];

      const [infraSnap, roomSnap, campusSnap] = await Promise.all([
        getDocs(collection(db, "Infrastructure")),
        getDocs(collection(db, "Rooms")),
        getDocs(collection(db, "Campus"))
      ]);
      infraSnap.forEach(doc => infraMap[doc.data().infra_id] = doc.data().name);
      roomSnap.forEach(doc => roomMap[doc.data().room_id] = doc.data().name);
      campusSnap.forEach(doc => campusMap[doc.data().campus_id] = doc.data().campus_name);

    } else {
      
      const mapRes = await fetch("../assets/firestore/MapVersions.json");
      const mapsJson = await mapRes.json();
      mapData = mapsJson.find(m => m.map_id === safeMapId) || mapsJson[0];
      if (!mapData) {
        try { const el = document.getElementById("map-overview")?.querySelector(".map-loader"); if (el) el.remove(); } catch(e){}
        return console.error("No maps found in JSON");
      }

      // Prefer selectedCampus from UI, then param, then stored value
      activeCampus = selectedCampus || campusId || mapData.current_active_campus;
      // Prefer selectedVersion from UI, then param, then stored value
      activeVersion = String(selectedVersion || versionId || mapData.current_version || (mapData.versions?.[0]?.id || ""));
      const versionData = mapData.versions.find(v => v.id === activeVersion);
      nodes = versionData ? versionData.nodes || [] : [];
      edges = versionData ? versionData.edges || [] : [];

      const [infraRes, roomRes, campusRes] = await Promise.all([
        fetch("../assets/firestore/Infrastructure.json"),
        fetch("../assets/firestore/Rooms.json"),
        fetch("../assets/firestore/Campus.json")
      ]);
      const infraJson = await infraRes.json();
      const roomJson = await roomRes.json();
      const campusJson = await campusRes.json();
      infraJson.forEach(i => infraMap[i.infra_id] = i.name);
      roomJson.forEach(r => roomMap[r.room_id] = r.name);
      campusJson.forEach(c => campusMap[c.campus_id] = c.campus_name);

      console.log("📂 Offline → Map, nodes, edges loaded from JSON");
    }

    
    
    
    
    if (showAllCampuses && Array.isArray(mapData.campus_included)) {
      
      console.log("🗺️ Displaying all campuses:", mapData.campus_included);
      nodes = nodes.filter(n => {
        if (n.is_deleted) return false;
        if (!n.campus_id) return false;
        return mapData.campus_included.includes(n.campus_id);
      });
    } else {
      
      nodes = nodes.filter(n => {
        if (n.is_deleted) return false;
        if (!n.campus_id) n.campus_id = activeCampus;
        return n.campus_id === activeCampus;
      });
    }

    
    
    
    const validNodeIds = new Set(nodes.map(n => n.node_id));
    edges = edges.filter(e =>
      !e.is_deleted &&
      validNodeIds.has(e.from_node) &&
      validNodeIds.has(e.to_node)
    );

    
    
    
    nodes.forEach(d => {
      d.infraName = d.related_infra_id ? (infraMap[d.related_infra_id] || d.related_infra_id) : "-";
      d.roomName = d.related_room_id ? (roomMap[d.related_room_id] || roomMap[d.related_room_id] || d.related_room_id) : "-";
      d.campusName = d.campus_id ? (campusMap[d.campus_id] || d.campus_id) : "-";
    });

    
    
    
    createOverviewMap(nodes, edges, activeCampus);

  } catch (err) {
    console.error("❌ Error loading map:", err);
    
    try { const el = document.getElementById("map-overview")?.querySelector(".map-loader"); if (el) el.remove(); } catch(e){}
  }
}




function createOverviewMap(nodes, edges, activeCampus) {
  
  if (mapOverview) {
    mapOverview.remove();
    document.getElementById("map-overview").innerHTML = "";
  }

  
  const container = document.getElementById("map-overview");
  if (!container) return;
  container.style.position = container.style.position || "relative";

  
  const overlay = document.createElement("div");
  overlay.className = "map-loader";
  overlay.innerHTML = `
    <div class="map-loader-inner">
      <div class="map-loader-spinner" aria-hidden="true"></div>
      <div class="map-loader-text">Loading Nodes...</div>
    </div>
  `;
  
  const prev = container.querySelector(".map-loader");
  if (prev) prev.remove();
  container.appendChild(overlay);

  
  mapOverview = L.map("map-overview", {
    zoomControl: true,
    dragging: true,
    scrollWheelZoom: false,
    doubleClickZoom: true,
    boxZoom: false,
    keyboard: false
  });

  const bounds = getCampusBounds(nodes, activeCampus);
  if (bounds) {
    mapOverview.fitBounds(bounds, { padding: [20, 20], maxZoom: 20, animate: true });
    mapOverview.setZoom(mapOverview.getZoom() + 0.4);
  } else {
    mapOverview.setView(getGeographicCenter(nodes, activeCampus), 18);
  }

  
  const tiles = L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    attribution: "© OpenStreetMap"
  }).addTo(mapOverview);

  
  tiles.on && tiles.on("load", () => {
    try { const el = container.querySelector(".map-loader"); if (el) el.remove(); } catch (e) {}
  });

  
  try {
    renderDataOnMap(mapOverview, { nodes, edges });
  } finally {
    
    try { const el = container.querySelector(".map-loader"); if (el) el.remove(); } catch (e) {}
  }

  
  
  
  const modal = document.getElementById("mapModal");
  const closeBtn = document.querySelector(".close-btn");

  document.getElementById("map-overview").addEventListener("click", () => {
    modal.style.display = "block";

    setTimeout(() => {
      if (mapFull) {
        mapFull.remove();
        document.getElementById("map-full").innerHTML = "";
      }

      const currentCenter = mapOverview.getCenter();
      const currentZoom = mapOverview.getZoom();

      mapFull = L.map("map-full", {
        center: currentCenter,
        zoom: currentZoom,
        zoomControl: true,
        dragging: true,
        scrollWheelZoom: true,
        doubleClickZoom: true,
        boxZoom: true,
        keyboard: true
      });

      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: "© OpenStreetMap"
      }).addTo(mapFull);

      renderDataOnMap(mapFull, { nodes, edges }, true);
    }, 200);
  });

  closeBtn.addEventListener("click", () => {
    modal.style.display = "none";
    if (mapFull) {
      mapFull.remove();
      mapFull = null;
    }
  });
}









































































































































































































































































const edgePolylines = new Map();

function renderDataOnMap(map, data, enableClick = false) {
  const nodes = Array.isArray(data.nodes) ? data.nodes : [];
  const edges = Array.isArray(data.edges) ? data.edges : [];


  
  const nodeMarkers = [];

  
  const barrierNodes = nodes.filter(d => d.type === "barrier");

  
  const barriersByCampus = {};
  barrierNodes.forEach(b => {
    const campusId = b.campus_id || "unknown";
    if (!barriersByCampus[campusId]) barriersByCampus[campusId] = [];
    barriersByCampus[campusId].push(b);
  });

  
  Object.entries(barriersByCampus).forEach(([campusId, barriers]) => {
    const barrierCoords = barriers.map(b => [b.latitude, b.longitude]);
    if (barrierCoords.length === 0) return;

    const center = {
      lat: barrierCoords.reduce((sum, c) => sum + c[0], 0) / barrierCoords.length,
      lng: barrierCoords.reduce((sum, c) => sum + c[1], 0) / barrierCoords.length
    };

    const sortedCoords = barrierCoords.slice().sort((a, b) => {
      const angleA = Math.atan2(a[1] - center.lng, a[0] - center.lat);
      const angleB = Math.atan2(b[1] - center.lng, b[0] - center.lat);
      return angleA - angleB;
    });

    const polygon = L.polygon(sortedCoords, {
      color: "green",
      weight: 3,
      fillOpacity: 0.1
    }).addTo(map);

    if (enableClick) {
      polygon.on("click", (e) => {
        showDetails({
          name: `Campus Area (${campusId})`,
          type: "Campus Area",
          latitude: e.latlng.lat.toFixed(6),
          longitude: e.latlng.lng.toFixed(6)
        });
      });

      barriers.forEach(node => {
        const cornerMarker = L.circleMarker([node.latitude, node.longitude], {
          radius: 6,
          color: "darkgreen",
          fillColor: "lightgreen",
          fillOpacity: 0.9
        }).addTo(map);

        
        nodeMarkers.push(cornerMarker);

        cornerMarker.on("click", () => showDetails(node));
      });
    }
  });

  
  
  const nodeMap = new Map();
  nodes.forEach(node => {
    if (node.node_id && node.latitude && node.longitude) {
      nodeMap.set(node.node_id, {
        coords: [node.latitude, node.longitude],
        type: node.type
      });
    }
  });

  
  edges.forEach(edge => {
    if (!edge.from_node || !edge.to_node) return;
    const from = nodeMap.get(edge.from_node);
    const to = nodeMap.get(edge.to_node);

    
    if (!from || !to || from.type === "barrier" || to.type === "barrier") return;

    const edgeActive = edge.is_active !== false;

    
    const line = L.polyline([from.coords, to.coords], {
      color: "orange",
      weight: edgeActive ? 3 : 2,
      opacity: edgeActive ? 0.8 : 0.18,
      interactive: true
    }).addTo(map);

    
    try { if (edge.edge_id) { line._edge_id = edge.edge_id; edgePolylines.set(edge.edge_id, line); } } catch(e){}

    
    line.on("mouseover", () => {
      try {
        if (edgeActive) {
          line.setStyle({ weight: 5, color: "rgba(250, 138, 46, 1)", opacity: 1.0 });
        } else {
          
          line.setStyle({ weight: 4, color: "#FFB347", opacity: 0.75 });
        }
      } catch (e) { /* ignore */ }
      try { map.getContainer().style.cursor = "pointer"; } catch(e){}
    });
    line.on("mouseout", () => {
      try {
        
        if (edgeActive) {
          line.setStyle({ weight: 3, color: "orange", opacity: 0.8 });
        } else {
          line.setStyle({ weight: 2, color: "orange", opacity: 0.48 });
        }
      } catch (e) { /* ignore */ }
      try { map.getContainer().style.cursor = ""; } catch(e){}
    });

    if (enableClick) {
      line.on("click", () => {
        showDetails({
          edge_id: edge.edge_id,
          from: edge.from_node,
          to: edge.to_node,
          distance: edge.distance,
          path_type: edge.path_type,
          elevations: edge.elevations,
          is_active: edge.is_active 
        });
      });
    }
  });

  
  nodes.filter(d => d.type === "infrastructure").forEach(building => {
    const marker = L.circleMarker([building.latitude, building.longitude], {
      radius: 6,
      color: "red",
      fillColor: "pink",
      fillOpacity: 0.7
    }).addTo(map);

    
    nodeMarkers.push(marker);

    if (enableClick) {
      marker.on("click", () => showDetails(building));
    }
  });

  
  nodes.filter(d => d.type === "room").forEach(room => {
    const marker = L.marker([room.latitude, room.longitude], { riseOnHover: true }).addTo(map);

    
    if (typeof marker.setZIndexOffset === "function") marker.setZIndexOffset(1000);
    nodeMarkers.push(marker);

    if (enableClick) marker.on("click", () => showDetails(room));
  });

  
  nodes.filter(d => d.type === "outdoor").forEach(outdoor => {
    const marker = L.circleMarker([outdoor.latitude, outdoor.longitude], {
      radius: 6,
      color: "red",
      fillColor: "pink",
      fillOpacity: 0.8
    }).addTo(map);

    nodeMarkers.push(marker);
    if (enableClick) marker.on("click", () => showDetails(outdoor));
  });

  
  nodes.filter(d => d.type === "intermediate").forEach(intermediate => {
    const marker = L.circleMarker([intermediate.latitude, intermediate.longitude], {
      radius: 3,
      color: "black",
      fillColor: "black",
      fillOpacity: 1.0
    }).addTo(map);

    nodeMarkers.push(marker);
    if (enableClick) marker.on("click", () => showDetails(intermediate));
  });

  
  nodeMarkers.forEach(m => {
    try {
      if (typeof m.bringToFront === "function") m.bringToFront();
      if (typeof m.setZIndexOffset === "function") m.setZIndexOffset(1000);
    } catch (e) { /* ignore */ }
  });
}









async function refreshModalMap() {
  try {
    if (!window.mapFull || !currentMapId) return;
    const modal = document.getElementById("mapModal");
    if (!modal) return;

    
    const overlay = document.createElement("div");
    overlay.className = "modal-map-refresh-overlay";
    overlay.style = "position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,0.85);z-index:9999;";
    overlay.innerHTML = `<div style="display:flex;flex-direction:column;align-items:center;gap:10px;">
      <div style="width:44px;height:44px;border-radius:50%;border:5px solid rgba(0,0,0,0.06);border-top-color:#DC143C;animation:spin 0.9s linear infinite"></div>
      <div style="color:#222;font-weight:700">Refreshing map…</div>
    </div>`;
    modal.appendChild(overlay);

    
    const mapDocRef = doc(db, "MapVersions", String(currentMapId));
    const mapDocSnap = await getDoc(mapDocRef);
    if (!mapDocSnap.exists()) { overlay.remove(); return; }
    const mapData = mapDocSnap.data();
    const versionId = mapData.current_version;
    const versionRef = doc(db, "MapVersions", String(currentMapId), "versions", versionId);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) { overlay.remove(); return; }
    const versionData = versionSnap.data();
    let nodes = Array.isArray(versionData.nodes) ? versionData.nodes : [];
    let edges = Array.isArray(versionData.edges) ? versionData.edges : [];

    
    const activeCampus = mapData.current_active_campus;
    nodes = nodes.filter(n => !n.is_deleted && (n.campus_id ? n.campus_id === activeCampus : true));
    const validNodeIds = new Set(nodes.map(n => n.node_id));
    edges = edges.filter(e => !e.is_deleted && validNodeIds.has(e.from_node) && validNodeIds.has(e.to_node));

    
    const center = window.mapFull.getCenter();
    const zoom = window.mapFull.getZoom();

    
    try { window.mapFull.remove(); } catch (e) {}
    document.getElementById("map-full").innerHTML = "";
    window.mapFull = L.map("map-full", { center, zoom, zoomControl: true, dragging: true, scrollWheelZoom: true, doubleClickZoom: true });
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { attribution: "© OpenStreetMap" }).addTo(window.mapFull);

    
    renderDataOnMap(window.mapFull, { nodes, edges }, true);
    overlay.remove();
  } catch (err) {
    console.warn("refreshModalMap failed:", err);
    try { document.querySelector(".modal-map-refresh-overlay")?.remove(); } catch(e){/*ignore*/}
  }
}







async function showDetails(node) {
  const sidebar = document.querySelector(".map-sidebar");

  
  sidebar.innerHTML = `
    <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;">
      <div style="width:48px;height:48px;border:5px solid rgba(0,0,0,0.08);border-top-color:#DC143C;border-radius:50%;animation:spin 0.8s linear infinite"></div>
      <div style="margin-top:12px;color:#666;font-weight:600">Loading details...</div>
    </div>
  `;

  
  if (node && (node.edge_id || (node.from && node.to))) {
    
    async function findNodeName(nodeId) {
      try {
        const mapsSnap = await getDocs(collection(db, "MapVersions"));
        for (const mapDoc of mapsSnap.docs) {
          const mapData = mapDoc.data();
          const currentVersion = mapData.current_version;
          if (!currentVersion) continue;
          const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
          const versionSnap = await getDoc(versionRef);
          if (!versionSnap.exists()) continue;
          const versionNodes = Array.isArray(versionSnap.data().nodes) ? versionSnap.data().nodes : [];
          const found = versionNodes.find(n => String(n.node_id) === String(nodeId) || String(n.related_room_id) === String(nodeId));
          if (found) return found.name || found.node_id || nodeId;
        }
      } catch (e) {
        console.warn("findNodeName error:", e);
      }
      return nodeId;
    }

    const fromId = node.from || node.from_node || "";
    const toId = node.to || node.to_node || "";
    const [fromName, toName] = await Promise.all([findNodeName(fromId), findNodeName(toId)]);

    
    const nice = (v) => v ? String(v).split("_").map(s => s[0].toUpperCase() + s.slice(1)).join(" ") : "-";
    const distanceText = (node.distance !== undefined && node.distance !== null) ? `${Number(node.distance).toFixed(2)} m` : "-";

    const edgeActive = node.is_active !== false;
    const edgeImg = edgeActive
      ? "../assets/imgs/pathway_active.png"
      : "../assets/imgs/pathway_inactive.png";

    sidebar.innerHTML = `
      <div style="padding:12px; display:flex;flex-direction:column;gap:12px;font-family:Inter, Arial, Helvetica, sans-serif;">
        <div style="border-radius:8px;overflow:hidden;box-shadow:0 8px 22px rgba(2,6,23,0.08);background:linear-gradient(180deg,#fff,#fff);">
          <div style="position:relative;height:170px;overflow:hidden;background:#f3f3f5">
            <img src="${edgeImg}" alt="Pathway" onerror="this.style.display='none'" style="width:100%;height:170px;object-fit:cover;display:block;"/>
            <div style="position:absolute;left:12px;bottom:12px;background:linear-gradient(90deg, rgba(220,20,60,0.12), rgba(0,0,0,0.04));backdrop-filter:blur(2px);padding:8px 10px;border-radius:999px;">
              <span style="font-weight:700;color:#7f1720;font-size:13px">Edge</span>
              <div style="font-size:12px;color:#3b3f45">${node.edge_id || "-"}</div>
            </div>
          </div>

          <div style="padding:12px 14px;display:flex;flex-direction:column;gap:10px;">
            <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px;">
              <div style="flex:1;min-width:0">
                <div style="font-size:13px;color:#6b7280;font-weight:700;margin-bottom:6px">From → To</div>
                <div id="edge-names" title="${escapeHtml(fromName + ' → ' + toName)}" style="font-size:15px;color:#0f1720;font-weight:700;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;text-overflow:ellipsis;cursor:pointer;">
                  ${escapeHtml(fromName)} <span style="color:#9aa0a6;font-weight:600;margin:0 8px">→</span> ${escapeHtml(toName)}
                </div>
                <div style="margin-top:6px;font-size:12px;color:#667085">${fromId}  •  ${toId}</div>
              </div>
            </div>

            <!-- moved toggle: clear, visible and near path/elevation -->
            <div style="display:flex;justify-content:flex-start;align-items:center;gap:10px;padding-top:6px;">
              <label style="display:inline-flex;align-items:center;gap:8px;font-size:13px;color:#374151;">
                <span style="font-size:13px;color:#6b7280;font-weight:700;margin-right:6px">Status</span>
                <input id="edge-active-input" type="checkbox" ${edgeActive ? "checked" : ""} style="width:0;height:0;opacity:0;position:absolute;">
                <span id="edge-active-switch" style="display:inline-block;width:46px;height:26px;border-radius:999px;background:${edgeActive ? "#DC143C" : "#e6e9ee"};position:relative;box-shadow:inset 0 1px 0 rgba(255,255,255,0.06);cursor:pointer;">
                  <span id="edge-active-thumb" style="position:absolute;top:4px;left:${edgeActive ? "24px" : "4px"};width:18px;height:18px;border-radius:50%;background:#fff;box-shadow:0 2px 6px rgba(2,6,23,0.12);transition:left 220ms ease"></span>
                </span>
              </label>
              <div id="edge-active-status" style="font-size:12px;color:#ffffff;font-weight:700;display:inline-block;padding:6px 10px;border-radius:999px;background:${edgeActive ? "rgba(220,20,60,0.14)" : "rgba(99,102,241,0.08)"};color:${edgeActive ? "#7f1720" : "#556070"}">
                ${edgeActive ? "Active" : "Inactive"}
              </div>
            </div>

            <div style="display:flex;gap:10px;align-items:stretch;margin-top:6px;">
              <div style="flex:1;background:linear-gradient(180deg,#ffffff,#fafafa);padding:10px;border-radius:8px;border:1px solid #eef2f7;box-shadow:inset 0 1px 0 rgba(255,255,255,0.6);">
                <div style="font-size:12px;color:#69717a;font-weight:700">Path type</div>
                <div style="margin-top:6px;font-size:14px;color:#0f1720;font-weight:600">${nice(node.path_type)}</div>
              </div>
              <div style="width:12px"></div>
              <div style="flex:1;background:linear-gradient(180deg,#ffffff,#fafafa);padding:10px;border-radius:8px;border:1px solid #eef2f7;">
                <div style="font-size:12px;color:#69717a;font-weight:700">Elevations</div>
                <div style="margin-top:6px;font-size:14px;color:#0f1720;font-weight:600">${nice(node.elevations)}</div>
              </div>
            </div>

          </div>
        </div>

        <div style="text-align:center;color:#6b7280;font-size:12px">This is an edge — QR codes only apply to nodes.</div>
      </div>
    `;

    
    try {
      const namesEl = sidebar.querySelector("#edge-names");
      if (namesEl) {
        namesEl.addEventListener("click", () => {
          const expanded = namesEl.classList.toggle("expanded");
          if (expanded) {
            namesEl.style.display = "block";
            namesEl.style.webkitLineClamp = "unset";
            namesEl.style.WebkitLineClamp = "unset";
            namesEl.style.whiteSpace = "normal";
            namesEl.style.overflow = "visible";
          } else {
            namesEl.style.display = "-webkit-box";
            namesEl.style.WebkitBoxOrient = "vertical";
            namesEl.style.webkitLineClamp = "2";
            namesEl.style.WebkitLineClamp = "2";
            namesEl.style.whiteSpace = "normal";
            namesEl.style.overflow = "hidden";
          }
        });
      }
     } catch (e) { /* ignore */ }
    
    try {
      const toggleSwitch = sidebar.querySelector("#edge-active-switch");
      const toggleInput = sidebar.querySelector("#edge-active-input");
      const statusEl = sidebar.querySelector("#edge-active-status");
      if (toggleSwitch && toggleInput && statusEl) {
        
        toggleSwitch.addEventListener("click", async (ev) => {
          ev.preventDefault();
          const newState = !toggleInput.checked;
          
          toggleInput.checked = newState;
          sidebar.querySelector("#edge-active-thumb").style.left = newState ? "20px" : "3px";
          toggleSwitch.style.background = newState ? "#DC143C" : "#e6e9ee";
          
          const prevText = statusEl.textContent;
          statusEl.innerHTML = `<span style="width:18px;height:18px;border:3px solid rgba(0,0,0,0.08);border-top-color:#DC143C;border-radius:50%;display:inline-block;animation:spin 0.8s linear infinite;"></span> Updating...`;

          try {
            
            await (async function toggleEdgeActive(edgeId, active) {
              
              const mapsSnap = await getDocs(collection(db, "MapVersions"));
              for (const mapDoc of mapsSnap.docs) {
                const mapData = mapDoc.data();
                const currentVersion = mapData.current_version;
                if (!currentVersion) continue;
                const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
                const versionSnap = await getDoc(versionRef);
                if (!versionSnap.exists()) continue;
                const versionData = versionSnap.data();
                const edgesArr = Array.isArray(versionData.edges) ? [...versionData.edges] : [];
                const idx = edgesArr.findIndex(e => e.edge_id === edgeId);
                if (idx !== -1) {
                  edgesArr[idx] = { ...edgesArr[idx], is_active: active, updated_at: new Date() };
                  await updateDoc(versionRef, { edges: edgesArr });
                  return;
                }
              }
              throw new Error("Edge not found in MapVersions");
            })(node.edge_id, newState);

            
            statusEl.textContent = newState ? "Active" : "Inactive";

            
            const poly = edgePolylines.get(node.edge_id);
            if (poly && typeof poly.setStyle === "function") {
              poly.setStyle({ opacity: newState ? 0.8 : 0.18, weight: newState ? 3 : 2 });
            }
            try { await refreshModalMap(); } catch(e) { /* ignore */ }
          } catch (err) {
            console.error("Failed to toggle edge active:", err);
            
            toggleInput.checked = !toggleInput.checked;
            sidebar.querySelector("#edge-active-thumb").style.left = toggleInput.checked ? "20px" : "3px";
            toggleSwitch.style.background = toggleInput.checked ? "#DC143C" : "#e6e9ee";
            statusEl.textContent = prevText || (toggleInput.checked ? "Active" : "Inactive");
            showModal('error', 'Failed to update edge status. See console for details.');
          }
        });
      }
    } catch (err) { console.warn("edge toggle init failed:", err); }

    return; 
  }

  if (node && (node.type === "Campus Area" || String(node.name || "").startsWith("Campus Area"))) {
    
    node.__isCampusArea = true;
    
    node.is_active = true;
    node.created_at = "1904";
    
    delete node.node_id;
  }

  
  let imageUrl = null;
  let infraEmail = "-";
  let infraPhone = "-";
  try {
    if (node && node.__isCampusArea) {
      imageUrl = "../assets/imgs/Western_Mindanao_State_University.png";
      infraEmail = "wmsu@wmsu.edu.ph";
      infraPhone = "991-1771";
    } else if (node.related_infra_id) {
      const q = query(collection(db, "Infrastructure"), where("infra_id", "==", node.related_infra_id));
      const snap = await getDocs(q);
      if (!snap.empty) {
        const infra = snap.docs[0].data();
        imageUrl = infra.image_url || null;
        infraEmail = infra.email || infraEmail;
        infraPhone = infra.phone || infraPhone;
      }
    }
  } catch (err) {
    console.warn("Failed to load infrastructure info for sidebar:", err);
  }

  
  let showRoomsLinkHtml = "";
  try {
    if (node.related_infra_id) {
      const infraId = String(node.related_infra_id);
      
      const indoorQ = query(collection(db, "IndoorInfrastructure"), where("infra_id", "==", infraId));
      const indoorSnap = await getDocs(indoorQ);
      if (!indoorSnap.empty) {
        showRoomsLinkHtml = `
          <a class="show-rooms-link" href="#" style="position:absolute;left:12px;bottom:12px;background:rgba(255,255,255,0.95);padding:6px 10px;border-radius:6px;color:#0f1720;font-weight:600;text-decoration:none;box-shadow:0 2px 6px rgba(2,6,23,0.12);display:inline-flex;align-items:center;gap:8px;">
            <i class="fas fa-th-large" style="color:#374151"></i>
            Show Rooms
          </a>`;
      } else {
        
        const roomsQ = query(collection(db, "Rooms"), where("infra_id", "==", infraId));
        const roomsSnap = await getDocs(roomsQ);
        if (!roomsSnap.empty) {
          showRoomsLinkHtml = `
            <a class="show-rooms-link" href="#" style="position:absolute;left:12px;bottom:12px;background:rgba(255,255,255,0.95);padding:6px 10px;border-radius:6px;color:#0f1720;font-weight:600;text-decoration:none;box-shadow:0 2px 6px rgba(2,6,23,0.12);display:inline-flex;align-items:center;gap:8px;">
              <i class="fas fa-th-large" style="color:#374151"></i>
              Show Rooms
            </a>`;
        }
      }
    }
  } catch (err) {
    console.warn("Failed to check rooms for infrastructure (show/hide Show Rooms):", err);
    
    showRoomsLinkHtml = "";
  }

  
  const placeholderSVG = 'data:image/svg+xml;utf8,' + encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="600" viewBox="0 0 1200 600">
       <rect width="100%" height="100%" fill="#f6f6f8"/>
       <g fill="#d1d3d8" font-family="Inter, Arial, Helvetica, sans-serif" font-size="26" text-anchor="middle">
         <text x="50%" y="50%" dy="0">No image available</text>
       </g>
     </svg>`
  );

  const imgSrc = imageUrl || placeholderSVG;

  
  let createdAtFormatted = "-";
  if (node.created_at) {
    try {
      if (typeof node.created_at.toDate === "function") {
        const d = node.created_at.toDate();
        createdAtFormatted = d.toLocaleString();
      } else if (node.created_at.seconds) {
        const d = new Date(node.created_at.seconds * 1000);
        createdAtFormatted = d.toLocaleString();
      } else {
        createdAtFormatted = String(node.created_at);
      }
    } catch (ex) {
      createdAtFormatted = String(node.created_at);
    }
  }

  
  const coordText = (node.longitude !== undefined && node.latitude !== undefined && node.longitude !== null && node.latitude !== null)
    ? `${Number(node.latitude).toFixed(6)}, ${Number(node.longitude).toFixed(6)}`
    : "-";

  
  const statusHtml = node.is_active
    ? `<span class="status-pill status-active" style="display:inline-flex;align-items:center;gap:6px;background:#e8f7ef;color:#0a7a4a;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-check-circle"></i> Active</span>`
    : `<span class="status-pill status-inactive" style="display:inline-flex;align-items:center;gap:6px;background:#fff3f3;color:#a33;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-times-circle"></i> Inactive</span>`;

  
  sidebar.innerHTML = `
    <div style="padding:12px; display:flex;flex-direction:column;gap:10px;">
      <div style="width:100%;display:flex;justify-content:center;">
        <div style="width:100%;max-width:320px;border-radius:8px;overflow:hidden;box-shadow:0 6px 18px rgba(9,30,66,0.08);position:relative;">
          <img id="sidebar-infra-image" src="${imgSrc}" alt="${(node.name||'Image').replace(/"/g,'')}" style="width:100%;height:220px;object-fit:cover;display:block;background:#f6f6f8" />
          ${showRoomsLinkHtml}
        </div>
      </div>

      <div style="text-align:center;padding:4px 8px;">
        <h3 style="margin:6px 0;font-size:18px;font-weight:700;color:#111">${node.name || "-"}</h3>
        <div style="color:#57606a;font-size:13px;">${node.node_id ? node.node_id : ""}</div>
      </div>

      <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:6px 0;border-radius:2px;"></div>

      <div style="display:flex;flex-direction:column;gap:8px;padding:0 6px;">
        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-map-marker-alt" style="color:#DC143C;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Coordinates</div>
            <div style="font-size:14px;color:#0f1720">${coordText}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-info-circle" style="color:#2b7a78;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Status</div>
            <div style="font-size:14px">${statusHtml}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-calendar-alt" style="color:#667085;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Created</div>
            <div style="font-size:14px;color:#0f1720;">${createdAtFormatted}</div>
          </div>
        </div>
      </div>

      <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:10px 0;border-radius:2px;"></div>

      <div style="display:flex;flex-direction:column;gap:8px;padding:0 6px;">
        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-envelope" style="color:#475569;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Email</div>
            <div style="font-size:14px;color:#0f1720;word-break:break-word">${infraEmail || "-"}</div>
          </div>
        </div>

        <div style="display:flex;align-items:center;gap:10px;">
          <i class="fas fa-phone" style="color:#475569;width:20px;text-align:center"></i>
          <div style="flex:1">
            <div style="font-size:12px;color:#69717a;font-weight:600">Phone</div>
            <div style="font-size:14px;color:#0f1720">${infraPhone || "-"}</div>
          </div>
        </div>
      </div>

      <div class="qr-section" style="padding-top:12px;display:flex;justify-content:center;"></div>
    </div>
  `;

  
  const showLink = document.querySelector(".map-sidebar .show-rooms-link");
  if (showLink) {
    showLink.addEventListener("click", (ev) => {
      ev.preventDefault();
      
      const infraId = node.related_infra_id || null;
      if (!infraId) {
        showModal('error', 'No related infrastructure recorded for this node.');
        return;
      }
      openRoomsModal({ infra_id: infraId, infra_node: node });
    });
  }

  


  if (node && node.node_id) {
    await renderQrSection(node);
  }
}



















const ROOM_ICON_SVGS = {
  
  room: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 24 24" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"><path d="M5 2h11a3 3 0 0 1 3 3v14a1 1 0 0 1-1 1h-3"/><path d="m5 2l7.588 1.518A3 3 0 0 1 15 6.459V20.78a1 1 0 0 1-1.196.98l-7.196-1.438A2 2 0 0 1 5 18.36V2Zm7 10v2"/></g></svg>`,
  
  stairs: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 24 24" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"><path d="M2 16h10v4H2zm2-4h10v4H4zm2-4h10v4H6zm2-4h10v4H8z"/><path d="M12 20h10V4h-4"/></g></svg>`,
  
  fire_exit: `<svg width="512" height="512" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8"><path fill="#000000" d="M4 3L3 5h1v3H3V6H2v1H0V6h1l1-3H1L0 4V2h4l1-1l-1-1l-1 1l2 2h1v1H5m2-3H6L5 0h3v8H7"/></svg>`
};

const _roomIconCache = {}; 

function svgToDataUrl(svg) {
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}

function loadIconImage(kind) {
  return new Promise((resolve) => {
    const key = kind || 'room';
    if (_roomIconCache[key] !== undefined) return resolve(_roomIconCache[key]);
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => { _roomIconCache[key] = img; resolve(img); };
    img.onerror = () => { _roomIconCache[key] = null; resolve(null); };
    img.src = svgToDataUrl(ROOM_ICON_SVGS[key] || ROOM_ICON_SVGS.room);
  });
}

let _roomIconsLoadedPromise = null;
function ensureRoomIconsLoaded() {
  if (_roomIconsLoadedPromise) return _roomIconsLoadedPromise;
  _roomIconsLoadedPromise = Promise.all([
    loadIconImage('room'),
    loadIconImage('stairs'),
    loadIconImage('fire_exit')
  ]).then(() => true).catch(() => true);
  return _roomIconsLoadedPromise;
}



























































































































































































async function openRoomsModal({ infra_id, infra_node = null } = {}) {
  document.querySelectorAll(".rooms-modal, .rooms-modal-backdrop").forEach(n => n.remove());
  await ensureRoomIconsLoaded();

  
  let infraName = infra_id;
  try {
    const q = query(collection(db, "Infrastructure"), where("infra_id", "==", infra_id));
    const snap = await getDocs(q);
    if (!snap.empty) infraName = snap.docs[0].data().name || infra_id;
  } catch (e) {
    console.warn("Could not fetch infra name for rooms modal:", e);
  }

  const backdrop = document.createElement("div");
  backdrop.className = "rooms-modal-backdrop";
  backdrop.style = "position:fixed;inset:0;background:rgba(6,18,31,0.45);z-index:10000;display:flex;align-items:center;justify-content:center;";
  document.body.appendChild(backdrop);


const modal = document.createElement("div");
modal.className = "rooms-modal";

modal.style = "background:#fff;border-radius:12px;padding:20px;width:1140px;max-width:96%;max-height:88vh;overflow:hidden;box-shadow:0 20px 60px rgba(2,6,23,0.35);display:flex;flex-direction:column;gap:12px;font-family:Inter,Arial,Helvetica,sans-serif;position:relative;";

modal.innerHTML = `
  <style>
    @keyframes spinCone { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .rooms-modal .rooms-body { display:flex; gap:16px; align-items:stretch; height:600px; } /* taller to accommodate larger canvas */
    .rooms-modal .rooms-canvas-wrap { flex:1; display:flex; flex-direction:column; align-items:center; justify-content:flex-start; position:relative; padding-top:6px; }
    .rooms-modal .rooms-canvas-container { position:relative; display:flex; align-items:center; justify-content:center; } /* holds canvas & overlay */
    .rooms-modal .rooms-canvas { width:900px; height:560px; border-radius:10px; border:1px solid #eef2f7; background:linear-gradient(180deg,#fcfdff,#fbfcfd); box-shadow:0 8px 24px rgba(9,30,66,0.04); display:block; }
    .rooms-modal .rooms-overlay { position:absolute; inset:0; pointer-events:none; } /* overlay now perfectly matches canvas container */
    .rooms-modal .rooms-legend { font-size:13px;color:#556070;width:100%;display:flex;justify-content:center;margin-top:10px; }
    .rooms-modal .details-panel { width:400px; min-width:320px; max-width:420px; border-left:1px solid #eef2f7; padding-left:18px; overflow:auto; background:linear-gradient(180deg,#ffffff,#fbfbfc); border-radius:8px; box-shadow: inset 0 1px 0 rgba(255,255,255,0.6); }
    .rooms-modal .details-panel .title { font-weight:700;color:#0f1720;font-size:15px; margin-bottom:6px; }
    .rooms-modal .details-panel .subtitle { color:#6b7280; font-size:13px; margin-bottom:12px; }
    .rooms-modal .details-panel img.room-img { width:100%; height:180px; object-fit:cover; border-radius:8px; box-shadow:0 8px 24px rgba(9,30,66,0.06); background:#f6f6f8; }
    .rooms-modal .details-panel .row { display:flex; gap:10px; align-items:center; margin-bottom:8px; }
    .rooms-modal .details-panel .label { font-size:12px;color:#69717a;font-weight:600; width:120px; }
    .rooms-modal .details-panel .value { font-size:14px;color:#0f1720; flex:1; }
    .rooms-modal .modal-qr-card { display:flex; justify-content:center; padding-top:6px; }
    .rooms-modal .rooms-header { display:flex; align-items:flex-start; justify-content:space-between; gap:8px; padding-bottom:6px; border-bottom:1px solid #eef2f7; }
    .rooms-modal .rooms-header .title-wrap { display:flex; align-items:center; gap:12px; }
    .rooms-modal .floor-controls { display:flex; align-items:center; gap:8px; margin-left:10px; }
    .rooms-modal .floor-controls button { border:none;background:#fff;border-radius:8px;padding:8px;cursor:pointer;box-shadow:0 2px 8px rgba(2,6,23,0.06); }
    .rooms-modal .close-btn { border:none;background:transparent;color:#556070;padding:8px;cursor:pointer;font-size:18px; margin-top:6px; border-radius:6px; }
    .rooms-modal .close-btn:hover { background:rgba(0,0,0,0.03); }
    .rooms-modal .room-marker { pointer-events:auto; position:absolute; display:inline-flex; align-items:center; justify-content:center; }
    .rooms-modal .room-marker.small { width:30px; height:30px; border-radius:8px; background:rgba(123,0,30,0.92); box-shadow:0 4px 10px rgba(0,0,0,0.12); border:none; cursor:pointer; }
    .rooms-modal .room-marker.small:focus { outline:3px solid rgba(123,0,30,0.15); }

    .rooms-modal .legend-item img { width:18px;height:18px;object-fit:contain;border-radius:3px; }
    @media (max-width: 1200px) {
      .rooms-modal { width: calc(100% - 40px); }
      .rooms-modal .rooms-canvas { width:760px; height:480px; }
      .rooms-modal .rooms-canvas-container { width:760px; height:480px; }
    }
    @media (max-width: 900px) {
      .rooms-modal .rooms-body { flex-direction:column; height:auto; }
      .rooms-modal .details-panel { width:100%; max-width:none; border-left:none; border-top:1px solid #eef2f7; padding-left:12px; padding-top:8px; }
      .rooms-modal .rooms-canvas { width:100%; height:420px; }
      .rooms-modal .rooms-canvas-container { width:100%; }
      .rooms-modal .rooms-overlay { inset:0; }
    }
  </style>

  <div class="rooms-header">
    <div class="title-wrap">
      <div style="display:flex;flex-direction:column;">
        <div style="font-weight:700;color:#0f1720;font-size:16px">Rooms — ${escapeHtml(infraName)}</div>
        <div id="rooms-floor-label" style="color:#6b7280;font-size:13px;margin-top:6px;">Floor • —</div>
      </div>

      <div class="floor-controls" role="group" aria-label="Floor controls">
        <button id="rooms-floor-prev" aria-label="Up (higher floor)" title="Up (higher floor)">
          <i class="fas fa-arrow-up" style="color:#374151"></i>
        </button>
        <button id="rooms-floor-next" aria-label="Down (lower floor)" title="Down (lower floor)">
          <i class="fas fa-arrow-down" style="color:#374151"></i>
        </button>
      </div>
    </div>

    <!-- Close button moved slightly lower for balance -->
    <button id="close-rooms-modal" class="close-btn" title="Close">
      <i class="fas fa-times"></i>
    </button>
  </div>

  <div class="rooms-body">
    <!-- LEFT: details panel (wider) -->
    <div class="details-panel" aria-live="polite">
      <div style="padding:14px;">
        <div class="title">Room Details</div>
        <div class="subtitle">Select a room on the map to view details</div>
        <div class="room-detail-content"></div>
      </div>
    </div>

    <!-- RIGHT: canvas + legend -->
    <div class="rooms-canvas-wrap">
      <div class="rooms-canvas-container">
        <canvas id="rooms-canvas" class="rooms-canvas" width="900" height="560"></canvas>
        <div class="rooms-overlay" aria-hidden="false"></div>
      </div>
      <div id="rooms-legend" class="rooms-legend"></div>
    </div>
  </div>

  <div id="rooms-loading-overlay" style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(255,255,255,0.88);z-index:40;">
    <div style="display:flex;flex-direction:column;align-items:center;gap:10px;">
      <div style="width:56px;height:56px;border-radius:50%;border:6px solid rgba(0,0,0,0.08);border-top-color:#DC143C;box-sizing:border-box;animation:spinCone 0.9s linear infinite;"></div>
      <div style="color:#000000;font-weight:700">Loading rooms…</div>
    </div>
  </div>
`;


  backdrop.appendChild(modal);

  modal.querySelector("#close-rooms-modal").addEventListener("click", () => closeRoomsModal());
  backdrop.addEventListener("click", (e) => { if (e.target === backdrop) closeRoomsModal(); });

  const loadingOverlay = modal.querySelector("#rooms-loading-overlay");
  if (loadingOverlay) loadingOverlay.style.display = "flex";

  
let roomNodes = [];
try {
  const mapsSnap = await getDocs(collection(db, "MapVersions"));
  for (const mapDoc of mapsSnap.docs) {
    const mapData = mapDoc.data();
    const currentVersion = mapData.current_version;
    if (!currentVersion) continue;

    const versionRef = doc(db, "MapVersions", mapDoc.id, "versions", currentVersion);
    const versionSnap = await getDoc(versionRef);
    if (!versionSnap.exists()) continue;

    const versionNodes = Array.isArray(versionSnap.data().nodes) ? versionSnap.data().nodes : [];

    for (const n of versionNodes) {
      const isIndoor = !!(n.indoor && (n.indoor.x !== undefined || n.indoor.y !== undefined || n.indoor.floor !== undefined));

      // NEW: check infra_id from IndoorInfrastructure if type is indoorinfra
      let matchesInfra = false;
      if (n.type === "indoorinfra" && infra_id) {
        const indoorSnap = await getDocs(query(collection(db, "IndoorInfrastructure"), where("infra_id", "==", infra_id)));
        matchesInfra = indoorSnap.docs.some(doc => String(doc.data().room_id || "") === String(n.related_room_id || n.node_id || ""));
      } else if (n.related_infra_id && infra_id) {
        matchesInfra = String(n.related_infra_id) === String(infra_id);
      }

      if (isIndoor && matchesInfra) roomNodes.push(Object.assign({}, n));
    }
  }
} catch (err) {
  console.error("Error loading room nodes for Show Rooms modal:", err);
}


  if (!roomNodes.length) {
    if (loadingOverlay) loadingOverlay.style.display = "none";
    modal.querySelector("#rooms-legend").textContent = "No room nodes found for this infrastructure.";
    renderRoomsFloor(modal, [], null, infra_node);
    return;
  }

  
  const indoorMap = {};
  try {
    const indoorSnap = await getDocs(collection(db, "IndoorInfrastructure"));
    indoorSnap.forEach(d => {
      const data = d.data();
      if (data.room_id) indoorMap[String(data.room_id)] = { indoor_type: data.indoor_type || data.type || "", name: data.name || data.room_name || "", infra_id: data.infra_id || null, image_url: data.image_url || data.image || null };
    });
  } catch (e) {
    console.warn("Failed to load IndoorInfrastructure map:", e);
  }

  
  const infraMap = {};
  try {
    const infraSnap = await getDocs(collection(db, "Infrastructure"));
    infraSnap.forEach(d => {
      const data = d.data();
      if (data.infra_id) infraMap[String(data.infra_id)] = data.name || data.infra_id;
    });
  } catch (e) {
    console.warn("Failed to load Infrastructure map:", e);
  }

  
  roomNodes = roomNodes.map(n => {
    const relatedRoomId = String(n.related_room_id || n.room_id || "");
    const indoorDoc = indoorMap[relatedRoomId] || {};
    const kind = (indoorDoc.indoor_type || n.indoor?.type || n.type || "").toString().toLowerCase();
    const roomName = indoorDoc.name || n.name || n.room_name || relatedRoomId || "Room";
    const roomInfraName = indoorDoc.infra_id ? (infraMap[String(indoorDoc.infra_id)] || indoorDoc.infra_id) : (infraMap[String(n.related_infra_id)] || n.related_infra_id || "");
    return Object.assign({}, n, { resolved_kind: kind, resolved_room_name: roomName, resolved_infra_name: roomInfraName, resolved_image: indoorDoc.image_url || null });
  });

  
  const floors = Array.from(new Set(roomNodes.map(r => (r.indoor?.floor ?? "0").toString()))).filter(Boolean);
  floors.sort((a,b) => {
    const na = Number(a), nb = Number(b);
    if (!isNaN(na) && !isNaN(nb)) return na - nb;
    return a.localeCompare(b);
  });

  let currentFloorIndex = 0;
  const setFloorIndex = (i) => {
    currentFloorIndex = Math.max(0, Math.min(floors.length - 1, i));
    const floor = floors[currentFloorIndex];
    modal.querySelector("#rooms-floor-label").textContent = `Floor ${floor}`;
    modal.querySelector("#rooms-floor-prev").disabled = (currentFloorIndex === floors.length - 1);
    modal.querySelector("#rooms-floor-next").disabled = (currentFloorIndex === 0);
    const roomsForFloor = roomNodes.filter(r => String(r.indoor?.floor ?? "0") === String(floor));
    renderRoomsFloor(modal, roomsForFloor, floor, infra_node);
  };

  modal.querySelector("#rooms-floor-prev").addEventListener("click", () => setFloorIndex(currentFloorIndex + 1));
  modal.querySelector("#rooms-floor-next").addEventListener("click", () => setFloorIndex(currentFloorIndex - 1));

  
  setFloorIndex(0);
  if (loadingOverlay) loadingOverlay.style.display = "none";

  
  
  
  function renderRoomsFloor(modalEl, roomsForFloor, floorLabel, infra_node = null) {
    const canvas = modalEl.querySelector("#rooms-canvas");
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    const W = canvas.width;
    const H = canvas.height;
    ctx.clearRect(0,0,W,H);

    
    ctx.fillStyle = "#fbfcfd";
    ctx.fillRect(0,0,W,H);

    const pad = 36;
    const drawW = W - pad*2;
    const drawH = H - pad*2;

    const infraOriginX = infra_node?.indoor?.x ? Number(infra_node.indoor.x) : 0;
    const infraOriginY = infra_node?.indoor?.y ? Number(infra_node.indoor.y) : 0;

    const points = roomsForFloor.map(r => {
      const rx = (r.indoor?.x !== undefined && r.indoor?.x !== null) ? Number(r.indoor.x) - infraOriginX : 0;
      const ry = (r.indoor?.y !== undefined && r.indoor?.y !== null) ? Number(r.indoor.y) - infraOriginY : 0;
      const kind = (r.resolved_kind || r.indoor?.type || r.indoor_type || r.type || "").toString().toLowerCase();
      const name = r.resolved_room_name || r.name || r.room_name || (r.related_room_id || r.room_id) || "?";
      const infraName = r.resolved_infra_name || "";

      return { raw: r, id: r.node_id || r.related_room_id || r.room_id || "?", name, infraName, x: rx, y: ry, kind };
    });

    const xs = points.map(p => p.x).concat([0]);
    const ys = points.map(p => p.y).concat([0]);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    const rangeX = (maxX - minX) || 1, rangeY = (maxY - minY) || 1;
    const scale = Math.min(drawW / rangeX, drawH / rangeY) * 0.9;
    const offsetX = pad + (drawW - (rangeX * scale)) / 2;
    const offsetY = pad + (drawH - (rangeY * scale)) / 2;

    const worldToCanvas = (x, y) => {
      const cx = offsetX + (x - minX) * scale;
      const cy = offsetY + (maxY - y) * scale;
      return [cx, cy];
    };

    
    ctx.strokeStyle = "rgba(14,39,75,0.045)";
    ctx.lineWidth = 1;
    const gridPx = 40;
    for (let gx = pad; gx <= W - pad; gx += gridPx) {
      ctx.beginPath(); ctx.moveTo(gx, pad); ctx.lineTo(gx, H - pad); ctx.stroke();
    }
    for (let gy = pad; gy <= H - pad; gy += gridPx) {
      ctx.beginPath(); ctx.moveTo(pad, gy); ctx.lineTo(W - pad, gy); ctx.stroke();
    }

    
    const [originCx, originCy] = worldToCanvas(0, 0);
    ctx.strokeStyle = "#e6eef6"; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.moveTo(originCx, pad); ctx.lineTo(originCx, H - pad); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(pad, originCy); ctx.lineTo(W - pad, originCy); ctx.stroke();
    ctx.fillStyle = "#0f1720"; ctx.beginPath(); ctx.arc(originCx, originCy, 4, 0, Math.PI*2); ctx.fill();
    ctx.fillStyle = "#6b7280"; ctx.font = "12px Inter, Arial, sans-serif"; ctx.fillText("0,0", originCx + 8, originCy - 10);

    
    const overlay = modalEl.querySelector(".rooms-overlay");
    overlay.innerHTML = "";
    overlay.style.pointerEvents = "none"; 
    overlay.style.width = W + "px";
    overlay.style.height = H + "px";

    
    points.forEach(p => {
      const [cx, cy] = worldToCanvas(p.x, p.y);
      const iconSize = Math.max(18, Math.min(40, Math.round(18 + Math.abs(p.x || 0) * 0.01)));

      
      drawIconImage(ctx, cx, cy, p.kind, iconSize);

      
      const nameText = p.name || p.id;
      const infraText = p.infraName || "";
      ctx.font = "13px Inter, Arial, sans-serif";
      const nameMetrics = ctx.measureText(nameText);
      const nameW = nameMetrics.width;
      ctx.font = "11px Inter, Arial, sans-serif";
      const infraMetrics = ctx.measureText(infraText);
      const infraW = infraMetrics.width;
      const tw = Math.max(nameW, infraW) + 16;
      const th = infraText ? 36 : 22;
      let tx = cx - tw / 2, ty = cy - (iconSize / 2) - 10 - th;
      tx = Math.max(pad - 6, Math.min(W - pad - tw + 6, tx));
      if (ty < 6) ty = cy + (iconSize / 2) + 8;
      ctx.fillStyle = "rgba(255,255,255,0.95)";
      roundRect(ctx, tx, ty, tw, th, 6, true, false);
      ctx.fillStyle = "#0f1720"; ctx.font = "13px Inter, Arial, sans-serif"; ctx.fillText(nameText, tx + 8, ty + 16);
      if (infraText) { ctx.fillStyle = "#6b7280"; ctx.font = "11px Inter, Arial, sans-serif"; ctx.fillText(infraText, tx + 8, ty + 30); }

      
      const btn = document.createElement("button");
      btn.className = "room-marker";
      btn.title = nameText;
      btn.style.position = "absolute";
      
      btn.style.left = `${Math.round(cx - 14)}px`;
      btn.style.top = `${Math.round(cy - 14)}px`;
      btn.style.width = `28px`;
      btn.style.height = `28px`;
      btn.style.borderRadius = "8px";
      btn.style.border = "none";
      btn.style.background = "rgba(123,0,30,0.0 )";
      btn.style.boxShadow = "0 2px 6px rgba(0,0,0,0.12)";
      btn.style.cursor = "pointer";
      btn.style.pointerEvents = "auto";
      btn.style.zIndex = "50";
      btn.setAttribute("aria-label", `Open details for ${nameText}`);
      btn.innerHTML = ""; 

      
      btn.addEventListener("click", (ev) => {
        ev.stopPropagation();
        renderRoomDetailsInModal(p.raw, modalEl);
      });

      overlay.appendChild(btn);
    });

    
    const legend = modalEl.querySelector("#rooms-legend");
    if (legend) {
      legend.innerHTML = `<div style="display:flex;gap:18px;align-items:center;color:#556070;">
        <div style="display:flex;align-items:center;gap:8px;" class="legend-item"><img src="${svgToDataUrl(ROOM_ICON_SVGS.room)}"/> Room</div>
        <div style="display:flex;align-items:center;gap:8px;" class="legend-item"><img src="${svgToDataUrl(ROOM_ICON_SVGS.stairs)}"/> Stairs</div>
        <div style="display:flex;align-items:center;gap:8px;" class="legend-item"><img src="${svgToDataUrl(ROOM_ICON_SVGS.fire_exit)}"/> Exit</div>
        <div style="margin-left:8px;color:#8b949e;font-size:12px;">${points.length} room(s) — Floor ${floorLabel ?? "-"}</div>
      </div>`;
    }
  }

  
  
  
  async function renderRoomDetailsInModal(room, modalEl) {
    const content = modalEl.querySelector(".room-detail-content");
    if (!content) return;

    
    content.innerHTML = `<div style="display:flex;flex-direction:column;align-items:center;justify-content:center;padding:12px;">
      <div style="width:36px;height:36px;border:4px solid rgba(0,0,0,0.08);border-top-color:#007bff;border-radius:50%;animation:spin 0.8s linear infinite"></div>
      <div style="margin-top:8px;color:#556070;font-weight:700">Loading room...</div>
    </div>`;

    const esc = s => String(s === null || s === undefined ? "" : s).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;");

    
    let infraEmail = "-";
    let infraPhone = "-";
    let collegeText = room.resolved_infra_name || "-";

    // Force the classroom image from assets
    const imgSrc = "../assets/imgs/classroom.jpg";

    try {
      const infraId = room.resolved_infra_id;
      if (infraId) {
        const q = query(collection(db, "Infrastructure"), where("infra_id", "==", String(infraId)));
        const snap = await getDocs(q);
        if (!snap.empty) {
          const infra = snap.docs[0].data();
          infraEmail = infra.email || infraEmail;
          infraPhone = infra.phone || infraPhone;
          collegeText = collegeText === "-" ? (infra.name || infra.college || infra.department || collegeText) : collegeText;
          // remove image override here — we always use classroom.jpg
        }
      }
    } catch (err) {
      console.warn("Failed to load infra contact for room details:", err);
    }


    
    let createdAtFormatted = "-";
    if (room.created_at) {
      try {
        if (typeof room.created_at.toDate === "function") createdAtFormatted = room.created_at.toDate().toLocaleString();
        else if (room.created_at.seconds) createdAtFormatted = new Date(room.created_at.seconds * 1000).toLocaleString();
        else createdAtFormatted = String(room.created_at);
      } catch (e) { createdAtFormatted = String(room.created_at); }
    }

    
    const getCoord = () => {
      const x = (room.indoor && room.indoor.x !== undefined && room.indoor.x !== null) ? Number(room.indoor.x).toFixed(6)
                : (room.x !== undefined && room.x !== null) ? Number(room.x).toFixed(6)
                : (room.x_coordinate !== undefined && room.x_coordinate !== null) ? String(room.x_coordinate)
                : (room.longitude !== undefined && room.longitude !== null) ? Number(room.longitude).toFixed(6)
                : null;
      const y = (room.indoor && room.indoor.y !== undefined && room.indoor.y !== null) ? Number(room.indoor.y).toFixed(6)
                : (room.y !== undefined && room.y !== null) ? Number(room.y).toFixed(6)
                : (room.y_coordinate !== undefined && room.y_coordinate !== null) ? String(room.y_coordinate)
                : (room.latitude !== undefined && room.latitude !== null) ? Number(room.latitude).toFixed(6)
                : null;
      return (y !== null && x !== null) ? `${y}, ${x}` : "-";
    };
    const coordText = getCoord();

    const statusHtml = room.is_active
      ? `<span style="display:inline-flex;align-items:center;gap:6px;background:#e8f7ef;color:#0a7a4a;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-check-circle"></i> Active</span>`
      : `<span style="display:inline-flex;align-items:center;gap:6px;background:#fff3f3;color:#a33;padding:6px 10px;border-radius:999px;font-weight:600;"><i class="fas fa-times-circle"></i> Inactive</span>`;

    
    content.innerHTML = `
      <div style="display:flex;flex-direction:column;gap:10px;">
        <div style="width:100%;display:flex;justify-content:center;">
          <div style="width:100%;max-width:280px;border-radius:8px;overflow:hidden;position:relative;">
            <img class="room-img" src="${imgSrc}" alt="${esc(room.resolved_room_name || room.name || 'Room')}" />
          </div>
        </div>

        <div>
          <h3 style="margin:6px 0;font-size:16px;font-weight:700;color:#111">${esc(room.resolved_room_name || room.name || "-")}</h3>
          <div style="color:#57606a;font-size:13px;margin-top:2px">${esc(room.node_id || "")}</div>
        </div>

        <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:6px 0;border-radius:2px;"></div>

        <div class="row"><div class="label">College / Department</div><div class="value">${esc(collegeText || "-")}</div></div>

        <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:6px 0;border-radius:2px;"></div>

        <div class="row"><div class="label">Coordinates (Y, X)</div><div class="value">${esc(coordText)}</div></div>
        <div class="row"><div class="label">Status</div><div class="value">${statusHtml}</div></div>
        <div class="row"><div class="label">Created</div><div class="value">${esc(createdAtFormatted)}</div></div>

        <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:8px 0;border-radius:2px;"></div>

        <div style="font-size:12px;color:#69717a;font-weight:600">Contact</div>
        <div style="font-size:14px;color:#0f1720;word-break:break-word">${esc(infraEmail || "-")}</div>
        <div style="font-size:14px;color:#0f1720">${esc(infraPhone || "-")}</div>

        <div style="height:1px;background:linear-gradient(90deg,#afafaf,#afafaf);margin:8px 0;border-radius:2px;"></div>

        <div class="modal-qr-card"></div>
      </div>
    `;

    
    try {
      const qrCard = content.querySelector(".modal-qr-card");
      if (!qrCard) return;

      qrCard.innerHTML = `<div style="display:flex;flex-direction:column;align-items:center;gap:8px;padding:8px;">
        <div style="width:28px;height:28px;border:4px solid rgba(0,0,0,0.08);border-top-color:#007bff;border-radius:50%;animation:spin 0.8s linear infinite"></div>
        <div style="color:#556070;font-size:13px;">Checking QR...</div>
      </div>`;

      const crimsonNodeId = `CRIMSON_${room.node_id}`;
      const qrRef = doc(db, "NodeQRCodes", crimsonNodeId);
      const qrSnap = await getDoc(qrRef);
      const hasQR = qrSnap.exists();
      const lastGenerated = hasQR ? (qrSnap.data().last_generated ? (new Date(qrSnap.data().last_generated.seconds ? qrSnap.data().last_generated.seconds * 1000 : qrSnap.data().last_generated).toLocaleString()) : "-") : "-";

      qrCard.innerHTML = `
        <div style="width:100%;max-width:260px;border-radius:8px;padding:10px;background:#fff;border:1px solid #eef2f7;box-shadow:0 6px 18px rgba(9,30,66,0.04);text-align:center;">
          <div style="font-size:13px;color:#394152;font-weight:700;margin-bottom:8px;">QR Code</div>
          <div style="margin-top:4px;color:#6b7280;font-size:12px;">${crimsonNodeId}<br><small>Last: ${lastGenerated}</small></div>
          <div style="display:flex;gap:8px;justify-content:center;margin-top:10px;">
            <button class="modal-btn-generate-qr" style="background:#007bff;color:#fff;border:none;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;">
              <i class="fas fa-qrcode" style="margin-right:6px"></i> Generate
            </button>
            ${hasQR ? `<button class="modal-btn-view-qr" style="background:#fff;border:1px solid #d1d5db;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;"><i class="fas fa-eye" style="margin-right:6px"></i> View</button>` : ''}
          </div>
        </div>
      `;

      
      const genBtn = qrCard.querySelector(".modal-btn-generate-qr");
      genBtn.addEventListener("click", async () => {
        try {
          const tempDiv = document.createElement("div");
          new QRCode(tempDiv, { text: crimsonNodeId, width: 512, height: 512, correctLevel: QRCode.CorrectLevel.H });
          await new Promise(res => setTimeout(res, 80));
          const canvas = tempDiv.querySelector("canvas") || tempDiv.querySelector("img");
          if (!canvas) throw new Error("QR render failed");
          const dataUrl = canvas.toDataURL("image/png");
          if (typeof openQrModal === "function") openQrModal(dataUrl, crimsonNodeId, canvas, room.resolved_room_name || room.name);
          await setDoc(qrRef, { node_id: crimsonNodeId, last_generated: new Date(), node_name: room.resolved_room_name || room.name || "-" }, { merge: true });
          
          await renderRoomDetailsInModal(room, modalEl);
        } catch (err) {
          console.error("QR generate error (modal):", err);
          showModal && showModal('error', 'Failed to generate QR code. Please try again.');
        }
      });

      
      const viewBtn = qrCard.querySelector(".modal-btn-view-qr");
      if (viewBtn) {
        viewBtn.addEventListener("click", async () => {
          try {
            const tempDiv = document.createElement("div");
            new QRCode(tempDiv, { text: crimsonNodeId, width: 512, height: 512, correctLevel: QRCode.CorrectLevel.H });
            await new Promise(res => setTimeout(res, 80));
            const canvas = tempDiv.querySelector("canvas") || tempDiv.querySelector("img");
            if (!canvas) throw new Error("QR render failed");
            const dataUrl = canvas.toDataURL("image/png");
            if (typeof openQrModal === "function") openQrModal(dataUrl, crimsonNodeId, canvas, room.resolved_room_name || room.name);
          } catch (err) {
            console.error("QR view error (modal):", err);
          }
        });
      }
    } catch (err) {
      console.warn("Modal QR render failed:", err);
    }
  }

  
  function roundRect(ctx, x, y, w, h, r, fill, stroke) {
    if (typeof r === 'undefined') r = 5;
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
    if (fill) ctx.fill();
    if (stroke) ctx.stroke();
  }
  function drawIconImage(ctx, cx, cy, kind = "", size = 18) {
    const k = (kind || "").toLowerCase();
    const key = (k.includes("stair") || k.includes("stairs")) ? "stairs" : (k.includes("fire") || k.includes("exit") ? "fire_exit" : "room");
    const img = _roomIconCache && _roomIconCache[key];
    if (img && img.width) {
      const targetW = size;
      const targetH = Math.round(img.height / img.width * targetW);
      ctx.drawImage(img, cx - targetW/2, cy - targetH/2, targetW, targetH);
      return;
    }
    ctx.save();
    ctx.beginPath();
    ctx.fillStyle = key === "stairs" ? "#B45309" : (key === "fire_exit" ? "#DC2626" : "#2563EB");
    ctx.arc(cx, cy, 6, 0, Math.PI*2);
    ctx.fill();
    ctx.restore();
  }
  function svgToDataUrl(svg) {
    return 'data:image/svg+xml;utf8,' + encodeURIComponent(svg);
  }

  
}




































































































































































function drawIconImage(ctx, cx, cy, kind = "", size = 18) {
  const k = (kind || "").toLowerCase();
  const key = (k.includes("stair") || k.includes("stairs")) ? "stairs" : (k.includes("fire") || k.includes("exit") ? "fire_exit" : "room");
  const img = _roomIconCache[key];

  if (img && img.width) {
    
    const targetW = size;
    const targetH = Math.round(img.height / img.width * targetW);
    ctx.drawImage(img, cx - targetW/2, cy - targetH/2, targetW, targetH);
    return;
  }

  
  ctx.save();
  ctx.beginPath();
  ctx.fillStyle = key === "stairs" ? "#B45309" : (key === "fire_exit" ? "#DC2626" : "#2563EB");
  ctx.arc(cx, cy, 6, 0, Math.PI*2);
  ctx.fill();
  ctx.restore();
}


function escapeHtml(str) {
  if (!str) return "";
  return String(str).replace(/&/g, "&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;");
}


function roundRect(ctx, x, y, w, h, r, fill, stroke) {
  if (typeof r === 'undefined') r = 5;
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
  if (fill) { ctx.fillStyle = ctx.fillStyle || "#fff"; ctx.fill(); }
  if (stroke) ctx.stroke();
}

function closeRoomsModal() {
  document.querySelectorAll(".rooms-modal, .rooms-modal-backdrop").forEach(n => n.remove());
}





























async function renderQrSection(node) {
  const qrSection = document.querySelector(".map-sidebar .qr-section");
  if (!qrSection) return;

  
  qrSection.innerHTML = `<div style="display:flex;flex-direction:column;align-items:center;gap:8px;padding:8px;">
    <div style="width:36px;height:36px;border:4px solid rgba(0,0,0,0.08);border-top-color:#007bff;border-radius:50%;animation:spin 0.8s linear infinite"></div>
    <div style="color:#556070;font-size:13px;">Checking QR...</div>
  </div>`;

  const crimsonNodeId = `CRIMSON_${node.node_id}`;
  const qrRef = doc(db, "NodeQRCodes", crimsonNodeId);
  const qrSnap = await getDoc(qrRef);
  const hasQR = qrSnap.exists();
  const lastGenerated = hasQR
    ? (qrSnap.data().last_generated
        ? (new Date(qrSnap.data().last_generated.seconds ? qrSnap.data().last_generated.seconds * 1000 : qrSnap.data().last_generated).toLocaleString())
        : "-")
    : "-";

  
  
  qrSection.innerHTML = `
    <div style="width:100%;max-width:300px;border-radius:8px;padding:12px;background:#fff;border:1px solid #eef2f7;box-shadow:0 6px 18px rgba(9,30,66,0.04);text-align:center;">
      <div style="font-size:13px;color:#394152;font-weight:700;margin-bottom:8px;">QR Code</div>



      <div style="margin-top:4px;color:#6b7280;font-size:12px;">${crimsonNodeId}<br><small>Last: ${lastGenerated}</small></div>

      <div style="display:flex;gap:8px;justify-content:center;margin-top:12px;">
        <button class="btn-generate-qr" style="background:#007bff;color:#fff;border:none;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;">
          <i class="fas fa-qrcode" style="margin-right:6px"></i> Generate
        </button>
        ${hasQR ? `<button class="btn-view-qr" style="background:#fff;border:1px solid #d1d5db;padding:8px 10px;border-radius:6px;cursor:pointer;font-weight:600;"><i class="fas fa-eye" style="margin-right:6px"></i> View</button>` : ''}
      </div>
    </div>
  `;

  
  const genBtn = qrSection.querySelector(".btn-generate-qr");
  genBtn.addEventListener("click", async () => {
    try {
      const qrDiv = document.createElement("div");
      new QRCode(qrDiv, {
        text: crimsonNodeId,
        width: 512,
        height: 512,
        correctLevel: QRCode.CorrectLevel.H
      });
      await new Promise(res => setTimeout(res, 80)); 
      const canvas = qrDiv.querySelector("canvas") || qrDiv.querySelector("img");
      const dataUrl = canvas.toDataURL("image/png");

      
      openQrModal(dataUrl, crimsonNodeId, canvas, node.name);


      
      await setDoc(qrRef, {
        node_id: crimsonNodeId,
        last_generated: new Date(),
        node_name: node.name || "-"
      }, { merge: true });

      
      await renderQrSection(node);
    } catch (err) {
      console.error("QR generate error:", err);
      showModal('error', 'Failed to generate QR code. Please try again.');
    }
  });

  const viewBtn = qrSection.querySelector(".btn-view-qr");
  if (viewBtn) {
    viewBtn.addEventListener("click", async () => {
      try {
        const qrDiv = document.createElement("div");
        new QRCode(qrDiv, {
          text: crimsonNodeId,
          width: 512,
          height: 512,
          correctLevel: QRCode.CorrectLevel.H
        });
        await new Promise(res => setTimeout(res, 80));
        const canvas = qrDiv.querySelector("canvas") || qrDiv.querySelector("img");
        const dataUrl = canvas.toDataURL("image/png");
        openQrModal(dataUrl, crimsonNodeId, canvas, node.name);
      } catch (err) {
        console.error(err);
      }
    });
  }
}

function openQrModal(qrDataUrl, nodeId, canvas = null, nodeName = "") {
  
  document.querySelector(".qr-modal")?.remove();

  const modal = document.createElement("div");
  modal.className = "qr-modal";
  Object.assign(modal.style, {
    position: "fixed",
    inset: "0",
    zIndex: "99999",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    background: "rgba(6,18,31,0.45)",
  });

  modal.innerHTML = `
    <div style="background:#fff;border-radius:12px;padding:18px;min-width:360px;max-width:820px;width:94%;
      box-shadow:0 12px 48px rgba(2,6,23,0.35);display:flex;flex-direction:column;gap:12px;align-items:center;">
      <div style="width:100%;display:flex;justify-content:space-between;align-items:center;">
        <div style="font-weight:700;color:#0f1720">${nodeName || nodeId}</div>
        <button class="qr-close" style="background:transparent;border:none;font-size:20px;cursor:pointer;color:#556070;">
          <i class="fas fa-times"></i>
        </button>
      </div>

      <div style="background:#fafafa;padding:12px;border-radius:10px;">
        <img src="${qrDataUrl}" alt="${nodeId}" style="width:320px;height:320px;object-fit:contain;display:block;"/>
      </div>

      <div style="display:flex;gap:10px;justify-content:center;width:100%;">
        <button id="download-qr-btn" style="background:#7b001e;color:#fff;border:none;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;">
          <i class="fas fa-download" style="margin-right:8px"></i> Download PNG
        </button>
        <button id="print-qr-btn" style="background:#fff;border:1px solid #d1d5db;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;color:#111;">
          <i class="fas fa-print" style="margin-right:8px"></i> Print
        </button>
        <button id="pdf-qr-btn" style="background:#111;color:#fff;border:none;padding:10px 14px;border-radius:8px;cursor:pointer;font-weight:700;">
          <i class="fas fa-file-pdf" style="margin-right:8px"></i> Export PDF
        </button>
      </div>
    </div>
  `;

  document.body.appendChild(modal);
  modal.querySelector(".qr-close").addEventListener("click", () => modal.remove());
  modal.addEventListener("click", (e) => { if (e.target === modal) modal.remove(); });

  

async function buildQrLayout() {
  const width = 800;
  const height = width; 
  const qrCanvas = document.createElement("canvas");
  qrCanvas.width = width;
  qrCanvas.height = height;
  const ctx = qrCanvas.getContext("2d");

  
  ctx.fillStyle = "#ffffff";
  ctx.fillRect(0, 0, width, height);

  
  const padding = 40;
  const boxX = padding;
  const boxY = padding;
  const boxW = width - padding * 2;
  const boxH = height - padding * 2;
  const radius = 20;

  ctx.fillStyle = "#7b001e";
  ctx.beginPath();
  ctx.moveTo(boxX + radius, boxY);
  ctx.lineTo(boxX + boxW - radius, boxY);
  ctx.quadraticCurveTo(boxX + boxW, boxY, boxX + boxW, boxY + radius);
  ctx.lineTo(boxX + boxW, boxY + boxH - radius);
  ctx.quadraticCurveTo(boxX + boxW, boxY + boxH, boxX + boxW - radius, boxY + boxH);
  ctx.lineTo(boxX + radius, boxY + boxH);
  ctx.quadraticCurveTo(boxX, boxY + boxH, boxX, boxY + boxH - radius);
  ctx.lineTo(boxX, boxY + radius);
  ctx.quadraticCurveTo(boxX, boxY, boxX + radius, boxY);
  ctx.closePath();
  ctx.fill();

  
  ctx.fillStyle = "#ffffffff";
  ctx.textAlign = "center";
  ctx.font = "bold 70px 'Poppins', sans-serif";
  const scanY = boxY + 90;
  ctx.fillText("SCAN HERE!", boxX + boxW / 2, scanY);

  
  const qrBoxSize = 340;
  const qrBoxX = boxX + (boxW - qrBoxSize) / 2;
  const qrBoxY = scanY + 40;
  const qrRadius = 20;

  ctx.fillStyle = "#ffffff";
  ctx.beginPath();
  ctx.moveTo(qrBoxX + qrRadius, qrBoxY);
  ctx.lineTo(qrBoxX + qrBoxSize - qrRadius, qrBoxY);
  ctx.quadraticCurveTo(qrBoxX + qrBoxSize, qrBoxY, qrBoxX + qrBoxSize, qrBoxY + qrRadius);
  ctx.lineTo(qrBoxX + qrBoxSize, qrBoxY + qrBoxSize - qrRadius);
  ctx.quadraticCurveTo(qrBoxX + qrBoxSize, qrBoxY + qrBoxSize, qrBoxX + qrBoxSize - qrRadius, qrBoxY + qrBoxSize);
  ctx.lineTo(qrBoxX + qrRadius, qrBoxY + qrBoxSize);
  ctx.quadraticCurveTo(qrBoxX, qrBoxY + qrBoxSize, qrBoxX, qrBoxY + qrBoxSize - qrRadius);
  ctx.lineTo(qrBoxX, qrBoxY + qrRadius);
  ctx.quadraticCurveTo(qrBoxX, qrBoxY, qrBoxX + qrRadius, qrBoxY);
  ctx.closePath();

  ctx.shadowColor = "rgba(0,0,0,0.12)";
  ctx.shadowBlur = 18;
  ctx.fill();
  ctx.shadowBlur = 0;

  
  const qrImg = new Image();
  qrImg.src = qrDataUrl;
  await new Promise(res => (qrImg.onload = res));
  const qrPadding = 20;
  const qrImgSize = qrBoxSize - qrPadding * 2;
  ctx.drawImage(qrImg, qrBoxX + qrPadding, qrBoxY + qrPadding, qrImgSize, qrImgSize);

  
  let footerY = qrBoxY + qrBoxSize + 50;
  ctx.textAlign = "center";

  
  ctx.font = "700 30px 'Poppins', sans-serif";
  ctx.fillStyle = "#ffffff";
  ctx.fillText("Western Mindanao State University", boxX + boxW / 2, footerY);

  
  ctx.font = "600 22px 'Poppins', sans-serif";
  ctx.fillStyle = "#fce8e8";
  footerY += 34;
  wrapAndDrawText(ctx, nodeName || nodeId, boxX + 60, footerY, boxW - 120, 28);

  
  ctx.font = "14px 'Poppins', sans-serif";
  ctx.fillStyle = "#f7dede";
  footerY += 40;
  ctx.fillText("Generated via CrimsonMap QR System", boxX + boxW / 2, footerY);

  
  const logo1 = new Image();
  const logo2 = new Image();
  logo1.src = "../assets/imgs/Western_Mindanao_State_University.png";
  logo2.src = "../assets/imgs/CrimsonMap Logo 1.png";
  await Promise.all([
    new Promise(res => (logo1.onload = res)),
    new Promise(res => (logo2.onload = res)),
  ]);

  const logoSize = 70;
  const logosY = footerY + 25;
  ctx.drawImage(logo1, boxX + (boxW / 2) - logoSize - 12, logosY, logoSize, logoSize);
  ctx.drawImage(logo2, boxX + (boxW / 2) + 12, logosY, logoSize, logoSize);

  
  ctx.strokeStyle = "rgba(255,255,255,0.08)";
  ctx.lineWidth = 1;
  ctx.strokeRect(boxX + 6, boxY + 6, boxW - 12, boxH - 12);

  return qrCanvas;
}



  
  function wrapAndDrawText(ctx, text, x, y, maxWidth, lineHeight) {
    
    const words = text.split(" ");
    let line = "";
    let curY = y;
    for (let n = 0; n < words.length; n++) {
      const testLine = line + words[n] + " ";
      const metrics = ctx.measureText(testLine);
      if (metrics.width > maxWidth && n > 0) {
        ctx.fillText(line.trim(), x + maxWidth / 2, curY);
        line = words[n] + " ";
        curY += lineHeight;
      } else {
        line = testLine;
      }
    }
    if (line) ctx.fillText(line.trim(), x + maxWidth / 2, curY);
  }

  
  function roundRectStroke(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.lineTo(x + w - r, y);
    ctx.quadraticCurveTo(x + w, y, x + w, y + r);
    ctx.lineTo(x + w, y + h - r);
    ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    ctx.lineTo(x + r, y + h);
    ctx.quadraticCurveTo(x, y + h, x, y + h - r);
    ctx.lineTo(x, y + r);
    ctx.quadraticCurveTo(x, y, x + r, y);
    ctx.closePath();
    ctx.stroke();
  }

  
  modal.querySelector("#download-qr-btn").addEventListener("click", async () => {
    const layoutCanvas = await buildQrLayout();
    const a = document.createElement("a");
    a.href = layoutCanvas.toDataURL("image/png");
    a.download = `${(nodeName || nodeId).replace(/\s+/g, "_")}_qr.png`;
    a.click();
  });

  modal.querySelector("#print-qr-btn").addEventListener("click", async () => {
    const layoutCanvas = await buildQrLayout();
    const dataUrl = layoutCanvas.toDataURL("image/png");
    const w = window.open("");
    w.document.write(`<img src="${dataUrl}" style="width:100%;height:auto;">`);
    w.document.close();
    w.focus();
    w.print();
    w.close();
  });

  modal.querySelector("#pdf-qr-btn").addEventListener("click", async () => {
    const layoutCanvas = await buildQrLayout();
    const imgData = layoutCanvas.toDataURL("image/png");
    const { jsPDF } = window.jspdf;
    
    const pdf = new jsPDF({ orientation: "portrait", unit: "px", format: "a4" });
    pdf.addImage(imgData, "PNG", 20, 20, 555, 760); 
    pdf.save(`${(nodeName || nodeId).replace(/\s+/g, "_")}_qr.pdf`);
  });
}

















function showModal(type, message) {
  const overlay = document.getElementById("jModal");
  const box = overlay.querySelector(".jModal-box");
  const icon = document.getElementById("jModal-icon");
  const title = document.getElementById("jModal-title");
  const msg = document.getElementById("jModal-message");
  const btn = document.getElementById("jModal-btn");

  
  box.classList.remove("jModal-success", "jModal-error");
  icon.innerHTML = "";

  
  let titleText = "";
  let iconSVG = "";

  if (type === "success") {
    box.classList.add("jModal-success");
    btn.style.background = "var(--jModal-success)";
    titleText = "Success";
    iconSVG = `
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="3" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="10" stroke="#28a745"/>
        <path d="M8 12.5l3 3 5-6" stroke="#28a745" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>`;
  } else {
    box.classList.add("jModal-error");
    btn.style.background = "var(--jModal-error)";
    titleText = "Error";
    iconSVG = `
      <svg xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="3" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="10" stroke="#dc3545"/>
        <path d="M15 9l-6 6M9 9l6 6" stroke="#dc3545" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>`;
  }

  
  icon.innerHTML = iconSVG;
  title.textContent = titleText;
  msg.textContent = message;

  
  overlay.classList.add("jModal-active");

  
  btn.onclick = () => {
    overlay.classList.remove("jModal-active");
  };
}