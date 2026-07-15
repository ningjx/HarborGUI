#!/usr/bin/env python3
"""
Docker Hub 镜像标签检查工具
自动读取当前目录下的 environment/Dockerfile，提取基础镜像并检查是否存在。
用法: python check-image-exist.py
"""

import re
import sys
from pathlib import Path

import requests


def find_dockerfile() -> Path:
    """在当前工作目录下查找 environment/Dockerfile"""
    dockerfile = Path.cwd() / "environment" / "Dockerfile"
    if not dockerfile.is_file():
        print(f"错误: 找不到 {dockerfile}", file=sys.stderr)
        sys.exit(0)
    return dockerfile


def extract_base_image(dockerfile_path: Path) -> str:
    """从 Dockerfile 中提取 FROM 指令指定的基础镜像（完整名称:tag）"""
    from_pattern = re.compile(r'^FROM\s+(\S+)', re.IGNORECASE | re.MULTILINE)
    content = dockerfile_path.read_text(encoding="utf-8")
    match = from_pattern.search(content)
    if not match:
        print("错误: Dockerfile 中未找到 FROM 指令", file=sys.stderr)
        sys.exit(0)
    return match.group(1)


def parse_image_ref(full_image: str) -> tuple[str, str]:
    """
    解析镜像引用，返回 (仓库路径, 标签)。
    例如: 'python:3.13-slim-bookworm' -> ('library/python', '3.13-slim-bookworm')
          'library/python:3.11'        -> ('library/python', '3.11')
          'alpine'                     -> ('library/alpine', 'latest')
    """
    if ":" in full_image:
        image, tag = full_image.rsplit(":", 1)
    else:
        image, tag = full_image, "latest"

    # 如果镜像名不含 /，则属于 library/ 官方仓库
    if "/" not in image:
        repo = f"library/{image}"
    else:
        repo = image

    return repo, tag


def check_tag_exists(repo: str, tag: str) -> bool:
    """检查指定仓库的标签是否存在于 Docker Hub"""
    url = f"https://hub.docker.com/v2/repositories/{repo}/tags"
    params = {"name": tag, "page_size": 1}

    try:
        resp = requests.get(url, params=params, timeout=15)
        resp.raise_for_status()
        data = resp.json()
        return data.get("count", 0) > 0
    except requests.RequestException as e:
        print(f"错误: 无法连接 Docker Hub API - {e}", file=sys.stderr)
        sys.exit(0)


def main() -> None:
    dockerfile_path = find_dockerfile()
    full_image = extract_base_image(dockerfile_path)
    repo, tag = parse_image_ref(full_image)

    exists = check_tag_exists(repo, tag)

    if exists:
        print("0")
    else:
        print(f"镜像 {full_image} 不存在")

    sys.exit(0)


if __name__ == "__main__":
    main()