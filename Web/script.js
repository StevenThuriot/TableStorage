(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('.copy-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const codeBlock = btn.closest('.code-block');
                const code = codeBlock.querySelector('code');
                if (!code) return;
                
                const text = code.textContent;

                navigator.clipboard.writeText(text).then(() => {
                    btn.textContent = 'Copied!';
                    btn.classList.add('copied');
                    setTimeout(() => {
                        btn.textContent = 'Copy';
                        btn.classList.remove('copied');
                    }, 2000);
                }).catch(err => {
                    console.error('Failed to copy: ', err);
                    btn.textContent = 'Failed';
                    setTimeout(() => {
                        btn.textContent = 'Copy';
                    }, 2000);
                });
            });
        });

        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', e => {
                const href = anchor.getAttribute('href');
                if (href === '#') return;

                const target = document.querySelector(href);
                if (target) {
                    e.preventDefault();
                    
                    // Respect user's motion preference
                    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                    target.scrollIntoView({ behavior: prefersReducedMotion ? 'auto' : 'smooth' });

                    // Update URL hash
                    history.pushState(null, '', href);

                    // Manage focus for accessibility
                    if (!target.hasAttribute('tabindex')) {
                        target.setAttribute('tabindex', '-1');
                    }
                    target.focus({ preventScroll: true });
                }
            });
        });
    });
})();
