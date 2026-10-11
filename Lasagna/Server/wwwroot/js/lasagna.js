(() => {
    const syncGroup = (group) => {
        const selector = group.dataset.requireOne;
        const inputs = Array.from(group.querySelectorAll(selector));
        const anchor = inputs[0];

        if (!anchor) {
            return;
        }

        const valid = inputs.some((input) => {
            if (input.type === "file") {
                return input.files && input.files.length > 0;
            }

            return input.checked;
        });

        anchor.setCustomValidity(
            valid
                ? ""
                : (group.dataset.validationMessage || "Choose at least one option.")
        );
    };

    const syncAllGroups = (root = document) => {
        root.querySelectorAll("[data-require-one]").forEach(syncGroup);
    };

    document.addEventListener("change", (event) => {
        const group = event.target.closest("[data-require-one]");
        if (group) {
            syncGroup(group);
        }
    });

    const initialize = () => {
        syncAllGroups();

        new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        syncAllGroups(node);
                    }
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
