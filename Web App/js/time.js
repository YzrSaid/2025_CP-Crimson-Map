

function updateDateTime() {
  const timeElement = document.querySelector(".time");

  if (!timeElement) return;

  
  const options = { timeZone: "Asia/Manila" };
  const now = new Date().toLocaleString("en-US", options);
  const dateObj = new Date(now);

  
  let hours = dateObj.getHours();
  const minutes = dateObj.getMinutes();
  const seconds = dateObj.getSeconds();
  const ampm = hours >= 12 ? "PM" : "AM";
  hours = hours % 12 || 12; 
  const formattedTime = `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")} ${ampm}`;

  
  const day = String(dateObj.getDate()).padStart(2, "0");
  const month = String(dateObj.getMonth() + 1).padStart(2, "0");
  const year = String(dateObj.getFullYear()).slice(-2);
  const formattedDate = `${day}-${month}-${year}`;

  
  timeElement.textContent = `${formattedTime} | ${formattedDate}`;
}


updateDateTime();
setInterval(updateDateTime, 1000);
