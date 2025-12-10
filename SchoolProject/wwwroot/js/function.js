/* ============================================================
   FUNCTION.JS – CLEAN, OPTIMIZED & GSAP SMOOTH SCROLL ENABLED
   ============================================================ */

(function ($) {
    "use strict";

    /* -------------------------------------------------
       1. REGISTER GSAP PLUGINS
    ------------------------------------------------- */
    gsap.registerPlugin(ScrollTrigger, ScrollToPlugin);

    // Disable browser's built-in smooth scroll
    document.documentElement.style.scrollBehavior = "auto";
    document.body.style.scrollBehavior = "auto";

    // Enable GSAP's scroll normalization
    ScrollTrigger.normalizeScroll(true);

    console.log("%cGSAP + ScrollToPlugin Loaded", "color: green;");

    /* -------------------------------------------------
       2. WINDOW & BODY VARIABLES
    ------------------------------------------------- */
    const $window = $(window);
    const $body = $("body");

    /* -------------------------------------------------
       3. PRELOADER
    ------------------------------------------------- */
    $window.on("load", function () {
        $(".preloader").fadeOut(600);

        // Refresh ScrollTrigger AFTER page load
        setTimeout(() => {
            ScrollTrigger.refresh();
            console.log("%cScrollTrigger Refreshed", "color: blue;");
        }, 200);
    });

    /* -------------------------------------------------
       4. STICKY HEADER
    ------------------------------------------------- */
    function setHeaderHeight() {
        const $header = $('header.active-sticky-header .header-sticky');
        $("header.active-sticky-header").css("height", $header.outerHeight());
    }

    if ($('.active-sticky-header').length) {
        $window.on("resize", setHeaderHeight);

        $window.on("scroll", function () {
            const fromTop = $window.scrollTop();
            setHeaderHeight();

            const headerHeight = $("header.active-sticky-header .header-sticky").outerHeight();

            $("header.active-sticky-header .header-sticky")
                .toggleClass("hide", fromTop > headerHeight + 100)
                .toggleClass("active", fromTop > 600);
        });
    }

    /* -------------------------------------------------
       5. RESPONSIVE MENU (Slicknav)
    ------------------------------------------------- */
    $("#menu").slicknav({
        label: "",
        prependTo: ".responsive-menu"
    });

    /* Scroll To Top */
    $(document).on("click", "a[href='#top']", function (e) {
        e.preventDefault();
        gsap.to(window, { scrollTo: 0, duration: 1.2, ease: "power3.out" });
    });

    /* -------------------------------------------------
       6. SWIPER SLIDERS
    ------------------------------------------------- */
    function initSwiper(selector, config) {
        if ($(selector).length) {
            new Swiper(selector + " .swiper", config);
        }
    }

    initSwiper(".testimonial-slider", {
        slidesPerView: 1,
        speed: 1000,
        spaceBetween: 30,
        loop: true,
        autoplay: { delay: 5000 },
        pagination: { el: ".testimonial-pagination", clickable: true },
        navigation: {
            nextEl: ".testimonial-button-next",
            prevEl: ".testimonial-button-prev"
        },
        breakpoints: {
            767: { slidesPerView: 2 },
            1024: { slidesPerView: 2 },
            1440: { slidesPerView: 3 }
        }
    });

    /* -------------------------------------------------
       7. SKILL BAR
    ------------------------------------------------- */
    if ($(".skills-progress-bar").length) {
        $(".skills-progress-bar").waypoint(function () {
            $(".skillbar").each(function () {
                $(this).find(".count-bar").animate({
                    width: $(this).attr("data-percent")
                }, 2000);
            });
        }, { offset: "70%" });
    }

    /* -------------------------------------------------
       8. COUNTER-UP
    ------------------------------------------------- */
    if ($(".counter").length) {
        $(".counter").counterUp({ delay: 6, time: 3000 });
    }

    /* -------------------------------------------------
       9. GSAP IMAGE REVEAL
    ------------------------------------------------- */
    if ($(".reveal").length) {
        document.querySelectorAll(".reveal").forEach((container) => {
            let image = container.querySelector("img");

            let tl = gsap.timeline({
                scrollTrigger: { trigger: container, toggleActions: "play none none none" }
            });

            tl.set(container, { autoAlpha: 1 })
                .from(container, { xPercent: -100, duration: 1, ease: "power2.out" })
                .from(image, { xPercent: 100, duration: 1, ease: "power2.out" }, "-=1");
        });
    }

    /* -------------------------------------------------
       10. TEXT ANIMATION (SplitText)
    ------------------------------------------------- */
    function initHeadingAnimation() {
        if (typeof SplitText === "undefined") {
            console.warn("SplitText NOT loaded.");
            return;
        }

        // Text-effect block
        $(".text-effect").each(function (_, el) {
            let split = new SplitText(el, {
                type: "lines,words,chars",
                linesClass: "split-line"
            });

            gsap.set(split.chars, { opacity: 0.3, x: -7 });

            gsap.to(split.chars, {
                scrollTrigger: {
                    trigger: el,
                    start: "top 90%",
                    scrub: 1
                },
                opacity: 1,
                x: 0,
                duration: 1,
                stagger: 0.15
            });
        });
    }

    if (document.fonts && document.fonts.ready)
        document.fonts.ready.then(initHeadingAnimation);
    else
        window.addEventListener("load", initHeadingAnimation);

    /* -------------------------------------------------
       11. MAGNIFIC POPUP
    ------------------------------------------------- */
    $(".gallery-items").magnificPopup({
        delegate: "a",
        type: "image",
        gallery: { enabled: true },
        mainClass: "mfp-with-zoom",
        zoom: {
            enabled: true,
            duration: 300,
            opener: (el) => el.find("img")
        }
    });

    /* -------------------------------------------------
       12. WOW JS
    ------------------------------------------------- */
    new WOW().init();

    /* -------------------------------------------------
       13. HOVER EFFECT HELPERS
    ------------------------------------------------- */
    function activateHover(parent) {
        const items = $(parent).children();
        items.on("mouseenter", function () {
            items.removeClass("active");
            $(this).addClass("active");
        });
    }

    activateHover(".fact-counter-item-list");
    activateHover(".program-item-list-prime");
    activateHover(".what-we-list-item-prime");
    activateHover(".how-works-item-list-prime");

})(jQuery);
