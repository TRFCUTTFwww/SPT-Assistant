"""Validate a native integration report; pass the exported repair-result.json."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("report", type=Path)
args = parser.parse_args()
data = json.loads(args.report.read_text(encoding="utf-8-sig"))
expected = {
    "63a39c7964283b5e9c56b280": "item_key_9",
    "637b60c3b7afa97bfc3d7001": "item_stimulator_obdolbos2_loot",
    "59e3556c86f7741776641ac2": "item_barter_household_bleach",
    "5bc9bc53d4351e00367fbcee": "item_barter_valuable_chicken",
    "5af0534a86f7743b6f354284": "item_barter_medical_diagset",
}
assert data["failed"] == 0, "Native render reported failure"
assert data["started"] == data["completed"] >= len(expected), "Incomplete queue"
assert data["maximumQueued"] >= len(expected), "Concurrent request batch was not exercised"
captures = {row["item"]: row for row in data["captures"]}
reports = {row["template"]: row for row in data["report"]}
for template, model in expected.items():
    row = captures[template]
    assert row["model"].removesuffix("(Clone)") == model, (template, row["model"])
    assert not row["cancelled"] and not row["faulted"], template
    assert reports[template]["sprite"] and reports[template]["after"], template
assert data["invalidations"] == 1, "Expected one explicit cache invalidation"
print(f"PASS: {data['completed']} native renders, five expected models, zero failures")
print("PNG contents and in-game appearance require separate visual inspection.")
