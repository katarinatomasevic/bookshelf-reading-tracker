# Image for both the embedding service and the one-off seed job. They share a Dockerfile because
# the seed script talks to the same model over HTTP and reuses the same corpus helpers; building
# a second image for a script that runs once would double the build time for nothing.
FROM python:3.12-slim

WORKDIR /app

# Where huggingface/sentence-transformers keeps downloaded models. Set before the download so the
# weights land in the image instead of in a throwaway home directory.
ENV HF_HOME=/opt/models \
    PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

# Install CPU-only torch first, from PyTorch's own index. The wheel published on PyPI pulls in
# the full CUDA runtime — several gigabytes of GPU support that can never be used here, since
# neither the container nor the marking machine has a GPU. Installing it up front also means the
# next pip install finds torch already satisfied and does not fetch the CUDA build behind our
# back as a dependency of sentence-transformers.
RUN pip install --no-cache-dir torch==2.5.1 --index-url https://download.pytorch.org/whl/cpu

COPY requirements.txt ./requirements.txt
COPY seed/requirements.txt ./seed-requirements.txt
RUN pip install --no-cache-dir -r requirements.txt -r seed-requirements.txt

# Bake the model into the image rather than downloading it on first start. The image grows by
# ~90 MB, and in exchange the container starts immediately, starts identically every time, and
# works with no internet access at all. Downloading at runtime would make the first start hang
# for a minute and fail outright whenever Hugging Face is unreachable — a bad trade for a project
# that has to be demonstrated live.
RUN python -c "from sentence_transformers import SentenceTransformer; \
    SentenceTransformer('sentence-transformers/all-MiniLM-L6-v2')"

# Enforced only after the download above: from here on the library is forbidden from reaching the
# network, which turns "the model is in the image" from an intention into a guarantee.
ENV HF_HUB_OFFLINE=1 \
    TRANSFORMERS_OFFLINE=1

COPY app ./app
COPY seed ./seed

EXPOSE 8000

# One worker on purpose. Each additional uvicorn worker is a separate process with its own copy
# of the model in memory, and this service is nowhere near CPU-bound enough to need them.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
