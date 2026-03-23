document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("analyze-form");
    const button = document.getElementById("analyze-button");
    const targetInput = document.getElementById("TargetHost");

    if (form && button) {
        form.addEventListener("submit", () => {
            button.disabled = true;
            button.classList.add("is-loading");
            document.body.classList.add("is-submitting");
        });
    }

    document.querySelectorAll("[data-view-target]").forEach((tab) => {
        tab.addEventListener("click", () => {
            const target = tab.getAttribute("data-view-target");

            document.querySelectorAll(".view-tab").forEach((item) => {
                item.classList.remove("is-active");
                item.setAttribute("aria-selected", "false");
            });

            document.querySelectorAll(".route-view").forEach((panel) => {
                panel.classList.remove("is-active");
            });

            tab.classList.add("is-active");
            tab.setAttribute("aria-selected", "true");
            document.getElementById(target)?.classList.add("is-active");
        });
    });

    if (targetInput) {
        document.querySelectorAll("[data-sample-target]").forEach((chip) => {
            chip.addEventListener("click", () => {
                const sampleTarget = chip.getAttribute("data-sample-target");
                if (!sampleTarget) {
                    return;
                }

                targetInput.value = sampleTarget;
                targetInput.focus();
                targetInput.select();
            });
        });
    }
});
