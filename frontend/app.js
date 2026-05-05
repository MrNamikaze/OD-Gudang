(function () {
  const { createApp } = Vue;
  const page = document.body.dataset.page;
  const csrfState = { token: "" };

  const navItems = [
    { key: "dashboard", label: "Dashboard", href: "dashboard.html" },
    { key: "monitoring", label: "Monitoring", href: "monitoring.html" },
    { key: "laporan", label: "Laporan", href: "laporan.html" },
    { key: "kamera", label: "Kamera", href: "kamera.html" },
    { key: "pengaturan", label: "Pengaturan", href: "pengaturan.html" }
  ];

  const fallbackCameras = [
    { id: "CAM_01", name: "Gate Inbound", location: "Pintu Masuk Gudang", status: "online", objectFocus: "person", latestObject: "person", stream: "rtsp://cam01/live" },
    { id: "CAM_02", name: "Aisle Forklift", location: "Jalur Forklift Area A", status: "online", objectFocus: "forklift", latestObject: "forklift", stream: "rtsp://cam02/live" },
    { id: "CAM_03", name: "Loading Dock", location: "Area Bongkar Muat", status: "online", objectFocus: "truck", latestObject: "truck", stream: "rtsp://cam03/live" },
    { id: "CAM_04", name: "Rack Storage", location: "Lorong Rak Tinggi", status: "offline", objectFocus: "person", latestObject: "person", stream: "rtsp://cam04/live" },
    { id: "CAM_05", name: "Packing Zone", location: "Zona Packing", status: "online", objectFocus: "pallet", latestObject: "pallet", stream: "rtsp://cam05/live" },
    { id: "CAM_06", name: "Outbound Gate", location: "Pintu Keluar Barang", status: "online", objectFocus: "truck", latestObject: "truck", stream: "rtsp://cam06/live" }
  ];

  const fallbackDetections = [
    { id: 1, timestampUtc: "2026-04-22T08:14:05Z", objectName: "person", cameraId: "CAM_01", zone: "Inbound", confidence: 0.96, imagePath: "/images/cam01-081405.jpg" },
    { id: 2, timestampUtc: "2026-04-22T08:20:51Z", objectName: "forklift", cameraId: "CAM_02", zone: "Aisle A", confidence: 0.91, imagePath: "/images/cam02-082051.jpg" },
    { id: 3, timestampUtc: "2026-04-22T08:32:17Z", objectName: "truck", cameraId: "CAM_03", zone: "Dock 2", confidence: 0.94, imagePath: "/images/cam03-083217.jpg" },
    { id: 4, timestampUtc: "2026-04-22T08:41:33Z", objectName: "person", cameraId: "CAM_05", zone: "Packing", confidence: 0.89, imagePath: "/images/cam05-084133.jpg" },
    { id: 5, timestampUtc: "2026-04-22T09:02:40Z", objectName: "pallet", cameraId: "CAM_05", zone: "Packing", confidence: 0.87, imagePath: "/images/cam05-090240.jpg" },
    { id: 6, timestampUtc: "2026-04-22T09:14:12Z", objectName: "truck", cameraId: "CAM_06", zone: "Outbound", confidence: 0.93, imagePath: "/images/cam06-091412.jpg" }
  ];
  const LIVE_FEED_REFRESH_MS = 1500;

  async function api(path, options = {}) {
    const response = await fetch(path, {
      credentials: "same-origin",
      cache: "no-store",
      headers: {
        "Content-Type": "application/json",
        ...(csrfState.token ? { "X-CSRF-TOKEN": csrfState.token } : {}),
        ...(options.headers || {})
      },
      ...options
    });

    if (response.status === 401) {
      return { unauthorized: true, ok: false };
    }

    if (response.status === 204) {
      return { ok: true, data: null };
    }

    const data = await response.json().catch(() => null);
    return { ok: response.ok, status: response.status, data };
  }

  async function ensureCsrfToken() {
    if (csrfState.token) {
      return csrfState.token;
    }

    const result = await api("/api/auth/csrf-token", {
      method: "GET",
      headers: {}
    });

    if (result.ok && result.data?.requestToken) {
      csrfState.token = result.data.requestToken;
    }

    return csrfState.token;
  }

  async function requireAuthPage() {
    const status = await api("/api/auth/status");

    if (page === "login") {
      if (status.ok && status.data?.isAuthenticated) {
        window.location.href = "dashboard.html";
        return false;
      }

      await ensureCsrfToken();
      return true;
    }

    if (!status.ok || !status.data?.isAuthenticated) {
      window.location.href = "login.html";
      return false;
    }

    await ensureCsrfToken();
    return status.data;
  }

  function formatTimestamp(value) {
    return new Date(value).toLocaleString("id-ID", {
      dateStyle: "medium",
      timeStyle: "short"
    });
  }

  function objectSummary(items) {
    const counts = {};
    items.forEach((item) => {
      counts[item.objectName] = (counts[item.objectName] || 0) + 1;
    });
    const top = Object.entries(counts).sort((a, b) => b[1] - a[1])[0];
    return top ? { name: top[0], count: top[1] } : { name: "-", count: 0 };
  }

  function busiestCamera(items) {
    const counts = {};
    items.forEach((item) => {
      counts[item.cameraId] = (counts[item.cameraId] || 0) + 1;
    });
    const top = Object.entries(counts).sort((a, b) => b[1] - a[1])[0];
    return top ? top[0] : "-";
  }

  function startOfToday() {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }

  function formatDateInputValue(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }

  function formatTimeInputValue(date) {
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${hours}:${minutes}`;
  }

  function createDefaultReportFilters() {
    const now = new Date();
    const oneHourBefore = new Date(now.getTime() - (60 * 60 * 1000));

    return {
      startDate: formatDateInputValue(oneHourBefore),
      endDate: formatDateInputValue(now),
      startTime: formatTimeInputValue(oneHourBefore),
      endTime: formatTimeInputValue(now),
      camera: "",
      object: ""
    };
  }

  if (page === "login") {
    requireAuthPage().then((shouldMount) => {
      if (!shouldMount) {
        return;
      }

      createApp({
        data() {
          return {
            username: "admin",
            password: "Admin!23456",
            error: "",
            loading: false
          };
        },
        methods: {
          async login() {
            this.loading = true;
            this.error = "";
            await ensureCsrfToken();

            let result = await api("/api/auth/login", {
              method: "POST",
              body: JSON.stringify({
                username: this.username,
                password: this.password
              })
            });

            if (!result.ok && result.status >= 500) {
              csrfState.token = "";
              await ensureCsrfToken();
              result = await api("/api/auth/login", {
                method: "POST",
                body: JSON.stringify({
                  username: this.username,
                  password: this.password
                })
              });
            }

            this.loading = false;

            if (!result.ok) {
              this.error = result.data?.message || "Login gagal.";
              return;
            }

            window.location.href = "dashboard.html";
          }
        }
      }).mount("#app");
    });

    return;
  }

  requireAuthPage().then((authData) => {
    if (!authData) {
      return;
    }

    createApp({
      data() {
        return {
          navItems,
          currentPage: page,
          currentUser: authData,
          cameras: fallbackCameras,
          recentDetections: [],
          dataSource: "database",
          liveImageVersion: Date.now(),
          refreshTimerId: null,
          filters: createDefaultReportFilters()
        };
      },
      computed: {
        todayDetections() {
          return this.recentDetections.length;
        },
        activeCameras() {
          return this.cameras.filter((camera) => camera.status === "online").length;
        },
        offlineCameras() {
          return this.cameras.filter((camera) => camera.status === "offline").length;
        },
        topObject() {
          return objectSummary(this.recentDetections);
        },
        averageConfidence() {
          const total = this.recentDetections.reduce((sum, item) => sum + Math.round(item.confidence * 100), 0);
          return Math.round(total / this.recentDetections.length);
        },
        growthRate() {
          return 18;
        },
        objectOptions() {
          return [...new Set(this.recentDetections.map((item) => item.objectName))];
        },
        filteredDetections() {
          return this.recentDetections;
        },
        filteredTopObject() {
          return objectSummary(this.filteredDetections);
        },
        busiestCamera() {
          return busiestCamera(this.filteredDetections);
        },
        liveCamera() {
          const latest = this.recentDetections[0];
          if (latest) {
            return {
              id: latest.cameraId,
              name: latest.cameraId,
              location: latest.zone,
              status: "online",
              objectFocus: latest.objectName
            };
          }

          return {
            id: "CAM_OBJECT_01",
            name: "Kamera Object Detection",
            location: "Feed dari program Python",
            status: "online",
            objectFocus: "live detection"
          };
        },
        liveFeedUrl() {
          return `/uploads/live/${this.liveCamera.id.toLowerCase()}_latest.jpg?v=${this.liveImageVersion}`;
        }
      },
      methods: {
        formatTimestamp,
        screenshotLabel(imagePath) {
          return imagePath ? imagePath.split("/").pop() : "preview.jpg";
        },
        async blobToDataUrl(blob) {
          return await new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onloadend = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(blob);
          });
        },
        imageExtensionFromPath(imagePath) {
          const normalized = (imagePath || "").toLowerCase();
          if (normalized.endsWith(".png")) {
            return "png";
          }

          return "jpeg";
        },
        buildDetectionsQuery() {
          const params = new URLSearchParams();

          if (this.filters.startDate) {
            const startTime = this.filters.startTime || "00:00";
            params.set("startUtc", `${this.filters.startDate}T${startTime}:00`);
          }

          if (this.filters.endDate) {
            const endTime = this.filters.endTime || "23:59";
            params.set("endUtc", `${this.filters.endDate}T${endTime}:59`);
          }

          if (this.filters.camera) {
            params.set("cameraId", this.filters.camera);
          }

          if (this.filters.object) {
            params.set("objectName", this.filters.object);
          }

          const query = params.toString();
          return query ? `/api/detections?${query}` : "/api/detections";
        },
        async loadDetections() {
          const result = await api("/api/detections");
          if (result.ok && Array.isArray(result.data)) {
            this.recentDetections = page === "laporan"
              ? result.data.slice(0, 10)
              : result.data;
            this.dataSource = "database";
            return;
          }

          this.recentDetections = page === "laporan"
            ? fallbackDetections.slice(0, 10)
            : fallbackDetections;
          this.dataSource = "fallback";
        },
        async applyReportFilters() {
          const result = await api(this.buildDetectionsQuery());
          if (result.ok && Array.isArray(result.data)) {
            this.recentDetections = result.data;
            this.dataSource = "database";
            return;
          }

          this.recentDetections = fallbackDetections;
          this.dataSource = "fallback";
        },
        startLiveRefresh() {
          if (page !== "monitoring") {
            return;
          }

          this.refreshTimerId = window.setInterval(() => {
            this.liveImageVersion = Date.now();
            this.loadDetections();
          }, LIVE_FEED_REFRESH_MS);
        },
        async logout() {
          await ensureCsrfToken();
          await api("/api/auth/logout", { method: "POST" });
          window.location.href = "login.html";
        },
        async exportReport() {
          const workbook = new ExcelJS.Workbook();
          const worksheet = workbook.addWorksheet("Laporan Deteksi");

          worksheet.columns = [
            { header: "Waktu", key: "timestamp", width: 24 },
            { header: "Objek", key: "objectName", width: 18 },
            { header: "Kamera", key: "cameraId", width: 18 },
            { header: "Zona", key: "zone", width: 18 },
            { header: "Confidence", key: "confidence", width: 14 },
            { header: "Screenshot", key: "screenshot", width: 24 }
          ];

          worksheet.getRow(1).font = { bold: true };
          worksheet.getRow(1).alignment = { vertical: "middle", horizontal: "center" };

          for (const item of this.filteredDetections) {
            const row = worksheet.addRow({
              timestamp: this.formatTimestamp(item.timestampUtc),
              objectName: item.objectName,
              cameraId: item.cameraId,
              zone: item.zone,
              confidence: `${Math.round(item.confidence * 100)}%`,
              screenshot: ""
            });

            row.height = 72;

            try {
              const imageResponse = await fetch(item.imagePath, {
                credentials: "same-origin",
                cache: "no-store"
              });
              if (imageResponse.ok) {
                const imageBlob = await imageResponse.blob();
                const dataUrl = await this.blobToDataUrl(imageBlob);
                const imageId = workbook.addImage({
                  base64: dataUrl,
                  extension: this.imageExtensionFromPath(item.imagePath)
                });

                worksheet.addImage(imageId, {
                  tl: { col: 5 + 0.12, row: row.number - 1 + 0.12 },
                  ext: { width: 110, height: 64 }
                });
              } else {
                row.getCell(6).value = "Gambar tidak tersedia";
              }
            } catch {
              row.getCell(6).value = "Gagal memuat gambar";
            }
          }

          worksheet.eachRow((row) => {
            row.eachCell((cell) => {
              cell.alignment = { vertical: "middle", horizontal: "left", wrapText: true };
              cell.border = {
                top: { style: "thin", color: { argb: "FFD9CFC1" } },
                left: { style: "thin", color: { argb: "FFD9CFC1" } },
                bottom: { style: "thin", color: { argb: "FFD9CFC1" } },
                right: { style: "thin", color: { argb: "FFD9CFC1" } }
              };
            });
          });

          const buffer = await workbook.xlsx.writeBuffer();
          const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          });
          const link = document.createElement("a");
          link.href = URL.createObjectURL(blob);
          link.download = "laporan-deteksi.xlsx";
          link.click();
          URL.revokeObjectURL(link.href);
        }
      },
      mounted() {
        this.loadDetections();
        this.startLiveRefresh();
      },
      beforeUnmount() {
        if (this.refreshTimerId) {
          window.clearInterval(this.refreshTimerId);
        }
      }
    }).mount("#app");
  });
})();
