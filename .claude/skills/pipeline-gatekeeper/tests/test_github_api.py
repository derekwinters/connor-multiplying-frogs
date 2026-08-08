"""Tests for page collection.

The rest of `_github_api` is network plumbing. This part is the logic that
silently loses data when it is wrong — a truncated read is a well-formed list,
so nothing downstream can tell it happened.
"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import _github_api as api  # noqa: E402


def pages(*sizes):
    """A fake `fetch_page` serving pages of the given sizes, and recording calls."""
    served = []
    number = [0]

    def fetch_page(page):
        served.append(page)
        size = sizes[page - 1] if page <= len(sizes) else 0
        batch = [{"n": number[0] + i} for i in range(size)]
        number[0] += size
        return batch

    fetch_page.served = served
    return fetch_page


class CollectPagesTests(unittest.TestCase):
    def test_a_single_short_page_is_returned_whole(self):
        fetch = pages(7)
        self.assertEqual(len(api.collect_pages(fetch, per_page=100)), 7)
        self.assertEqual(fetch.served, [1])

    def test_a_full_page_is_followed_by_another_request(self):
        """The bug this exists to prevent: stopping at exactly 100."""
        fetch = pages(100, 28)
        self.assertEqual(len(api.collect_pages(fetch, per_page=100)), 128)
        self.assertEqual(fetch.served, [1, 2])

    def test_several_full_pages_are_all_collected(self):
        fetch = pages(100, 100, 5)
        self.assertEqual(len(api.collect_pages(fetch, per_page=100)), 205)

    def test_an_exactly_full_last_page_costs_one_extra_empty_request(self):
        """Unavoidable: a full page is indistinguishable from a truncated one."""
        fetch = pages(100)
        self.assertEqual(len(api.collect_pages(fetch, per_page=100)), 100)
        self.assertEqual(fetch.served, [1, 2])

    def test_an_empty_first_page_is_an_empty_result(self):
        self.assertEqual(api.collect_pages(pages(0), per_page=100), [])

    def test_a_none_page_is_treated_as_empty_rather_than_raising(self):
        self.assertEqual(api.collect_pages(lambda page: None, per_page=100), [])

    def test_items_keep_their_order_across_pages(self):
        collected = api.collect_pages(pages(3, 2), per_page=3)
        self.assertEqual([item["n"] for item in collected], [0, 1, 2, 3, 4])

    def test_the_page_cap_stops_a_runaway(self):
        """A stop-condition bug must not spin against the API forever."""
        fetch = pages(*([100] * 500))
        collected = api.collect_pages(fetch, per_page=100, max_pages=3)

        self.assertEqual(len(collected), 300)
        self.assertEqual(fetch.served, [1, 2, 3])


if __name__ == "__main__":
    unittest.main()
