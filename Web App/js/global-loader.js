const StatLoader = {
  
  start(selector) {
    document.querySelectorAll(selector).forEach(el => {
      
      if (el.dataset.loading === "true") return;

      el.dataset.originalText = el.textContent;
      el.dataset.loading = "true";

      
      el.textContent = "";
      const shimmer = document.createElement("span");
      shimmer.classList.add("stat-loading");
      el.appendChild(shimmer);
    });
  },

  
  stop(selector, values = {}) {
    document.querySelectorAll(selector).forEach(el => {
      const key = el.dataset.stat;
      const value = values[key] ?? el.dataset.originalText ?? "—";

      el.dataset.loading = "false";
      el.innerHTML = value;
    });
  }
};




function showUniversalLoader(container, type = "default") {
  if (!container) return;

  
  container.innerHTML = "";

  
  if (type === "table") {
    const tr = document.createElement("tr");
    tr.classList.add("table-loader");
    const td = document.createElement("td");
    td.colSpan = 10; 
    td.innerHTML = `<div class="universal-loader"><div class="spinner"></div></div>`;
    tr.appendChild(td);
    container.appendChild(tr);
  } else {
    container.innerHTML = `<div class="universal-loader"><div class="spinner"></div></div>`;
  }
}


function hideUniversalLoader(container) {
  if (container) container.innerHTML = "";
}





function showMapLoader(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;
  
  
  container.style.position = "relative";

  
  let loader = container.querySelector(".map-loading-overlay");
  if (!loader) {
    loader = document.createElement("div");
    loader.className = "map-loading-overlay";
    loader.innerHTML = `<div class="spinner"></div>`;
    container.appendChild(loader);
  }
  loader.style.display = "flex";
}

function hideMapLoader(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;
  const loader = container.querySelector(".map-loading-overlay");
  if (loader) loader.style.display = "none";
}






function showDropdownLoader(selectId) {
  const select = document.getElementById(selectId);
  if (!select) return;
  const wrapper = select.closest('.select-loading');
  if (wrapper) wrapper.classList.add('loading');
  select.disabled = true;
}

function hideDropdownLoader(selectId) {
  const select = document.getElementById(selectId);
  if (!select) return;
  const wrapper = select.closest('.select-loading');
  if (wrapper) wrapper.classList.remove('loading');
  select.disabled = false;
}
