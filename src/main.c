#include "efi.h"
#include "types.h"
#include "config.h"
#include "util.h"

/**
 * The version.
 */
#ifdef GIT_DESCRIBE_W
	const CHAR16 version[] = GIT_DESCRIBE_W;
#else
	const CHAR16 version[] = L"unknown; not an official release?";
#endif

EFI_SYSTEM_TABLE *ST;
EFI_BOOT_SERVICES *BS;
EFI_RUNTIME_SERVICES *RT;

/**
 * The configuration.
 */
static struct HackBGRT_config config = {
	.log = 1,
	.animation_path = L"\\EFI\\HackBGRT\\animation\\",
	.animation_prefix = L"frame_",
	.animation_digits = 3,
	.animation_ext = L".bmp",
	.animation_fps = 24,
	.animation_max_ms = 3500,
	.animation_final_last = 1,
	.animation_clear_each_frame = 1,
	.animation_preload = 1,
	.animation_max_preload_mb = 64,
	.animation_allow_frame_skip = 0,
};

/**
 * Get the GOP (Graphics Output Protocol) pointer.
 */
static EFI_GRAPHICS_OUTPUT_PROTOCOL* GOP(void) {
	static EFI_GRAPHICS_OUTPUT_PROTOCOL* gop;
	if (!gop) {
		LibLocateProtocol(TmpGuidPtr((EFI_GUID) EFI_GRAPHICS_OUTPUT_PROTOCOL_GUID), (void**) &gop);
	}
	return gop;
}

/**
 * Set screen resolution. If there is no exact match, try to find a bigger one.
 *
 * @param w Horizontal resolution. 0 for max, -1 for current.
 * @param h Vertical resolution. 0 for max, -1 for current.
 */
static void SetResolution(int w, int h) {
	EFI_GRAPHICS_OUTPUT_PROTOCOL* gop = GOP();
	if (!gop) {
		if (config.resolution_x <= 0 || config.resolution_y <= 0) {
			config.resolution_x = 1024;
			config.resolution_y = 768;
		}
		config.old_resolution_x = config.resolution_x;
		config.old_resolution_y = config.resolution_y;
		Log(config.debug, L"GOP not found! Assuming resolution %dx%d.\n", config.resolution_x, config.resolution_y);
		return;
	}
	UINTN best_i = gop->Mode->Mode;
	int best_w = config.old_resolution_x = gop->Mode->Info->HorizontalResolution;
	int best_h = config.old_resolution_y = gop->Mode->Info->VerticalResolution;
	w = (w <= 0 ? w < 0 ? best_w : 999999 : w);
	h = (h <= 0 ? h < 0 ? best_h : 999999 : h);

	Log(config.debug, L"Looking for resolution %dx%d...\n", w, h);
	for (UINT32 i = gop->Mode->MaxMode; i--;) {
		int new_w = 0, new_h = 0;

		EFI_GRAPHICS_OUTPUT_MODE_INFORMATION* info = 0;
		UINTN info_size;
		if (EFI_ERROR(gop->QueryMode(gop, i, &info_size, &info))) {
			continue;
		}
		if (info_size < sizeof(*info)) {
			BS->FreePool(info);
			continue;
		}
		new_w = info->HorizontalResolution;
		new_h = info->VerticalResolution;
		BS->FreePool(info);

		// Sum of missing w/h should be minimal.
		int new_missing = max(w - new_w, 0) + max(h - new_h, 0);
		int best_missing = max(w - best_w, 0) + max(h - best_h, 0);
		if (new_missing > best_missing) {
			continue;
		}
		// Sum of extra w/h should be minimal.
		int new_over = max(-w + new_w, 0) + max(-h + new_h, 0);
		int best_over = max(-w + best_w, 0) + max(-h + best_h, 0);
		if (new_missing == best_missing && new_over >= best_over) {
			continue;
		}
		best_w = new_w;
		best_h = new_h;
		best_i = i;
	}
	Log(config.debug, L"Found resolution %dx%d.\n", best_w, best_h);
	config.resolution_x = best_w;
	config.resolution_y = best_h;
	if (best_i != gop->Mode->Mode) {
		gop->SetMode(gop, best_i);
	}
}

/**
 * Create a new XSDT with the given number of entries.
 *
 * @param xsdt0 The old XSDT.
 * @param entries The number of SDT entries.
 * @return Pointer to a new XSDT.
 */
ACPI_SDT_HEADER* CreateXsdt(ACPI_SDT_HEADER* xsdt0, UINTN entries) {
	ACPI_SDT_HEADER* xsdt = 0;
	UINT32 xsdt_len = sizeof(ACPI_SDT_HEADER) + entries * sizeof(UINT64);
	BS->AllocatePool(EfiACPIReclaimMemory, xsdt_len, (void**)&xsdt);
	if (!xsdt) {
		Log(1, L"Failed to allocate memory for XSDT.\n");
		return 0;
	}
	BS->SetMem(xsdt, xsdt_len, 0);
	BS->CopyMem(xsdt, xsdt0, min(xsdt0->length, xsdt_len));
	xsdt->length = xsdt_len;
	SetAcpiSdtChecksum(xsdt);
	return xsdt;
}

/**
 * Update the ACPI tables as needed for the desired BGRT change.
 *
 * If action is REMOVE, all BGRT entries will be removed.
 * If action is KEEP, the first BGRT entry will be returned.
 * If action is REPLACE, the given BGRT entry will be stored in each XSDT.
 *
 * @param action The intended action.
 * @param bgrt The BGRT, if action is REPLACE.
 * @return Pointer to the BGRT, or 0 if not found (or destroyed).
 */
static ACPI_BGRT* HandleAcpiTables(enum HackBGRT_action action, ACPI_BGRT* bgrt) {
	for (int i = 0; i < ST->NumberOfTableEntries; i++) {
		EFI_GUID* vendor_guid = &ST->ConfigurationTable[i].VendorGuid;
		if (CompareMem(vendor_guid, TmpGuidPtr((EFI_GUID) ACPI_TABLE_GUID), sizeof(EFI_GUID)) != 0 && CompareMem(vendor_guid, TmpGuidPtr((EFI_GUID) ACPI_20_TABLE_GUID), sizeof(EFI_GUID)) != 0) {
			continue;
		}
		ACPI_20_RSDP* rsdp = (ACPI_20_RSDP *) ST->ConfigurationTable[i].VendorTable;
		if (CompareMem(rsdp->signature, "RSD PTR ", 8) != 0 || rsdp->revision < 2 || !VerifyAcpiRsdp2Checksums(rsdp)) {
			continue;
		}
		Log(config.debug, L"RSDP @%x: revision = %d, OEM ID = %s\n", (UINTN)rsdp, rsdp->revision, TmpStr(rsdp->oem_id, 6));

		ACPI_SDT_HEADER* xsdt = (ACPI_SDT_HEADER *) (UINTN) rsdp->xsdt_address;
		if (!xsdt || CompareMem(xsdt->signature, "XSDT", 4) != 0 || !VerifyAcpiSdtChecksum(xsdt)) {
			Log(config.debug, L"* XSDT: missing or invalid\n");
			continue;
		}
		UINT64* entry_arr = (UINT64*)&xsdt[1];
		UINT32 entry_arr_length = (xsdt->length - sizeof(*xsdt)) / sizeof(UINT64);

		Log(config.debug, L"* XSDT @%x: OEM ID = %s, entry count = %d\n", (UINTN)xsdt, TmpStr(xsdt->oem_id, 6), entry_arr_length);

		int bgrt_count = 0;
		for (int j = 0; j < entry_arr_length; j++) {
			ACPI_SDT_HEADER *entry = (ACPI_SDT_HEADER *)((UINTN)entry_arr[j]);
			if (CompareMem(entry->signature, "BGRT", 4) != 0) {
				continue;
			}
			Log(config.debug, L" - ACPI table @%x: %s, revision = %d, OEM ID = %s\n", (UINTN)entry, TmpStr(entry->signature, 4), entry->revision, TmpStr(entry->oem_id, 6));
			switch (action) {
				case HackBGRT_ACTION_KEEP:
					if (!bgrt) {
						Log(config.debug, L" -> Returning this one for later use.\n");
						bgrt = (ACPI_BGRT*) entry;
					}
					break;
				case HackBGRT_ACTION_REMOVE:
					Log(config.debug, L" -> Deleting.\n");
					for (int k = j+1; k < entry_arr_length; ++k) {
						entry_arr[k-1] = entry_arr[k];
					}
					--entry_arr_length;
					entry_arr[entry_arr_length] = 0;
					xsdt->length -= sizeof(entry_arr[0]);
					--j;
					break;
				case HackBGRT_ACTION_REPLACE:
					Log(config.debug, L" -> Replacing.\n");
					entry_arr[j] = (UINTN) bgrt;
			}
			bgrt_count += 1;
		}
		if (!bgrt_count && action == HackBGRT_ACTION_REPLACE && bgrt) {
			Log(config.debug, L" - Adding missing BGRT.\n");
			xsdt = CreateXsdt(xsdt, entry_arr_length + 1);
			entry_arr = (UINT64*)&xsdt[1];
			entry_arr[entry_arr_length++] = (UINTN) bgrt;
			rsdp->xsdt_address = (UINTN) xsdt;
			SetAcpiRsdp2Checksums(rsdp);
		}
		SetAcpiSdtChecksum(xsdt);
	}
	return bgrt;
}

/**
 * Generate a BMP with the given size and color.
 *
 * @param w The width.
 * @param h The height.
 * @param r The red component.
 * @param g The green component.
 * @param b The blue component.
 * @return The generated BMP, or 0 on failure.
 */
static BMP* MakeBMP(int w, int h, UINT8 r, UINT8 g, UINT8 b) {
	BMP* bmp = 0;
	BS->AllocatePool(EfiBootServicesData, 54 + w * h * 4, (void**) &bmp);
	if (!bmp) {
		Log(1, L"Failed to allocate a blank BMP!\n");
		BS->Stall(1000000);
		return 0;
	}
	*bmp = (BMP) {
		.magic_BM = { 'B', 'M' },
		.file_size = 54 + w * h * 4,
		.pixel_data_offset = 54,
		.dib_header_size = 40,
		.width = w,
		.height = h,
		.planes = 1,
		.bpp = 32,
	};
	UINT8* data = (UINT8*) bmp + bmp->pixel_data_offset;
	for (int y = 0; y < h; ++y) for (int x = 0; x < w; ++x) {
		*data++ = b;
		*data++ = g;
		*data++ = r;
		*data++ = 0;
	}
	return bmp;
}

static BOOLEAN ParseBMPGeometry(const BMP* bmp, INT32* width, INT32* height, BOOLEAN* top_down) {
	const INT32 w = (INT32) bmp->width;
	const INT32 h = (INT32) bmp->height;
	if (w <= 0 || h == 0 || h == (INT32) 0x80000000) {
		return FALSE;
	}
	*width = w;
	*height = h < 0 ? -h : h;
	*top_down = h < 0;
	return TRUE;
}

static BOOLEAN ValidateBMPData(const BMP* bmp, UINTN size, UINT32* row_size, INT32* width, INT32* height, BOOLEAN* top_down) {
	if (!bmp || size < sizeof(BMP) || CompareMem(bmp, "BM", 2) != 0 || bmp->planes != 1) {
		return FALSE;
	}
	if (bmp->compression != 0 || (bmp->bpp != 24 && bmp->bpp != 32)) {
		return FALSE;
	}
	if (!ParseBMPGeometry(bmp, width, height, top_down)) {
		return FALSE;
	}
	if (bmp->pixel_data_offset >= size || bmp->pixel_data_offset >= bmp->file_size || bmp->file_size > size) {
		return FALSE;
	}
	if ((UINT32) (*width) > 0xffffffffU / (UINT32) bmp->bpp) {
		return FALSE;
	}
	const UINT32 row_bits = (UINT32) (*width) * (UINT32) bmp->bpp;
	const UINT32 padded_row = ((row_bits + 31U) / 32U) * 4U;
	if (padded_row == 0 || (UINT32) (*height) > 0xffffffffU / padded_row) {
		return FALSE;
	}
	const UINT32 pixel_bytes = padded_row * (UINT32) (*height);
	if (bmp->pixel_data_offset > bmp->file_size - pixel_bytes) {
		return FALSE;
	}
	if (bmp->pixel_data_offset > size - pixel_bytes) {
		return FALSE;
	}
	*row_size = padded_row;
	return TRUE;
}

/**
 * Load a bitmap or generate a black one.
 *
 * @param base_dir The directory for loading a BMP.
 * @param path The BMP path within the directory; NULL for a black BMP.
 * @return The loaded BMP, or 0 if not available.
 */
static BMP* LoadBMP(EFI_FILE_HANDLE base_dir, const CHAR16* path) {
	if (!path) {
		return MakeBMP(1, 1, 0, 0, 0); // empty path = black image
	}
	Log(config.debug, L"Loading %s.\n", path);
	UINTN size = 0;
	BMP* bmp = LoadFile(base_dir, path, &size);
	if (bmp) {
		UINT32 row_size = 0;
		INT32 width = 0;
		INT32 height = 0;
		BOOLEAN top_down = FALSE;
		if (ValidateBMPData(bmp, size, &row_size, &width, &height, &top_down) && !top_down) {
			return bmp;
		}
		BS->FreePool(bmp);
		Log(1, L"Invalid BMP (%s)!\n", path);
	} else {
		Log(1, L"Failed to load BMP (%s)!\n", path);
	}
	BS->Stall(1000000);
	return MakeBMP(16, 16, 255, 0, 0); // error = red image
}

/**
 * Crop a BMP to the given size.
 *
 * @param bmp The BMP to crop.
 * @param w The maximum width.
 * @param h The maximum height.
 */
static void CropBMP(BMP* bmp, int w, int h) {
	INT32 signed_w = 0;
	INT32 signed_h = 0;
	BOOLEAN top_down = FALSE;
	if (!ParseBMPGeometry(bmp, &signed_w, &signed_h, &top_down) || top_down) {
		Log(1, L"CropBMP: unsupported BMP orientation.\n");
		return;
	}
	const int old_pitch = -(-(signed_w * (bmp->bpp / 8)) & ~3);
	bmp->image_size = 0;
	bmp->width = (UINT32) min(signed_w, w);
	bmp->height = (UINT32) min(signed_h, h);
	const int new_pitch = -(-(bmp->width * (bmp->bpp / 8)) & ~3);

	if (new_pitch < old_pitch) {
		for (int i = 1; i < bmp->height; ++i) {
			BS->CopyMem(
				(UINT8*) bmp + bmp->pixel_data_offset + i * new_pitch,
				(UINT8*) bmp + bmp->pixel_data_offset + i * old_pitch,
				new_pitch
			);
		}
	}
	bmp->file_size = bmp->pixel_data_offset + bmp->height * new_pitch;
}

/**
 * Build full animation frame path from config + frame index.
 */
static BOOLEAN BuildAnimationFramePath(CHAR16* out, UINTN out_len, UINT32 frame) {
	if (!out || out_len < 2 || config.animation_digits <= 0) {
		return FALSE;
	}
	const CHAR16* path = config.animation_path ? config.animation_path : L"";
	const CHAR16* prefix = config.animation_prefix ? config.animation_prefix : L"";
	const CHAR16* ext = config.animation_ext ? config.animation_ext : L"";

	UINTN pos = 0;
	for (UINTN i = 0; path[i]; ++i) {
		if (pos + 1 >= out_len) {
			return FALSE;
		}
		out[pos++] = path[i];
	}
	if (pos && out[pos - 1] != L'\\' && out[pos - 1] != L'/') {
		if (pos + 1 >= out_len) {
			return FALSE;
		}
		out[pos++] = L'\\';
	}
	for (UINTN i = 0; prefix[i]; ++i) {
		if (pos + 1 >= out_len) {
			return FALSE;
		}
		out[pos++] = prefix[i];
	}

	UINT32 tmp = frame;
	for (int i = config.animation_digits - 1; i >= 0; --i) {
		if (pos + i + 1 >= out_len) {
			return FALSE;
		}
		out[pos + i] = L'0' + (tmp % 10);
		tmp /= 10;
	}
	if (tmp) {
		return FALSE;
	}
	pos += config.animation_digits;

	for (UINTN i = 0; ext[i]; ++i) {
		if (pos + 1 >= out_len) {
			return FALSE;
		}
		out[pos++] = ext[i];
	}
	out[pos] = 0;
	return TRUE;
}

/**
 * Load a BMP for animation. Returns 0 on any error.
 */
static BMP* LoadAnimationBMP(EFI_FILE_HANDLE base_dir, const CHAR16* path) {
	UINTN size = 0;
	BMP* bmp = LoadFile(base_dir, path, &size);
	if (!bmp) {
		return 0;
	}
	UINT32 row_size = 0;
	INT32 width = 0;
	INT32 height = 0;
	BOOLEAN top_down = FALSE;
	if (!ValidateBMPData(bmp, size, &row_size, &width, &height, &top_down)) {
		BS->FreePool(bmp);
		return 0;
	}
	return bmp;
}

struct AnimationBltFrame {
	EFI_GRAPHICS_OUTPUT_BLT_PIXEL* blt;
	UINT32 width;
	UINT32 height;
	UINTN blt_bytes;
};

static BOOLEAN DecodeBMPToBltFrame(BMP* bmp, struct AnimationBltFrame* out) {
	if (!bmp || !out) {
		return FALSE;
	}
	UINT32 row_size = 0;
	INT32 width = 0;
	INT32 height = 0;
	BOOLEAN top_down = FALSE;
	if (!ValidateBMPData(bmp, bmp->file_size, &row_size, &width, &height, &top_down)) {
		return FALSE;
	}
	const UINTN pixel_count = (UINTN) width * (UINTN) height;
	const UINTN blt_bytes = pixel_count * sizeof(EFI_GRAPHICS_OUTPUT_BLT_PIXEL);
	EFI_GRAPHICS_OUTPUT_BLT_PIXEL* blt = 0;
	if (EFI_ERROR(BS->AllocatePool(EfiBootServicesData, blt_bytes, (void**)&blt))) {
		return FALSE;
	}
	const UINT8* src_base = (const UINT8*) bmp + bmp->pixel_data_offset;
	const UINTN src_bytes_per_pixel = bmp->bpp / 8;
	for (INT32 y = 0; y < height; ++y) {
		const INT32 src_y = top_down ? y : (height - 1 - y);
		const UINT8* src_row = src_base + ((UINTN) src_y * row_size);
		EFI_GRAPHICS_OUTPUT_BLT_PIXEL* dst_row = blt + ((UINTN) y * width);
		for (INT32 x = 0; x < width; ++x) {
			const UINT8* src = src_row + ((UINTN) x * src_bytes_per_pixel);
			dst_row[x].Blue = src[0];
			dst_row[x].Green = src[1];
			dst_row[x].Red = src[2];
			dst_row[x].Reserved = src_bytes_per_pixel == 4 ? src[3] : 0;
		}
	}
	*out = (struct AnimationBltFrame) {
		.blt = blt,
		.width = (UINT32) width,
		.height = (UINT32) height,
		.blt_bytes = blt_bytes,
	};
	return TRUE;
}

static void FreeBltFrame(struct AnimationBltFrame* frame) {
	if (frame && frame->blt) {
		BS->FreePool(frame->blt);
		frame->blt = 0;
		frame->width = 0;
		frame->height = 0;
		frame->blt_bytes = 0;
	}
}

static const CHAR16* GopPixelFormatName(EFI_GRAPHICS_PIXEL_FORMAT fmt) {
	switch (fmt) {
		case PixelBlueGreenRedReserved8BitPerColor: return L"PixelBlueGreenRedReserved8BitPerColor";
		case PixelRedGreenBlueReserved8BitPerColor: return L"PixelRedGreenBlueReserved8BitPerColor";
		case PixelBitMask: return L"PixelBitMask";
		case PixelBltOnly: return L"PixelBltOnly";
		default: return L"Unknown";
	}
}

static BOOLEAN DrawBltFrame(EFI_GRAPHICS_OUTPUT_PROTOCOL* gop, const struct AnimationBltFrame* frame, int clear_each_frame) {
	if (!gop || !gop->Mode || !gop->Mode->Info || !frame || !frame->blt || !frame->width || !frame->height) {
		return FALSE;
	}
	const UINTN screen_w = gop->Mode->Info->HorizontalResolution;
	const UINTN screen_h = gop->Mode->Info->VerticalResolution;
	const UINTN draw_w = frame->width <= screen_w ? frame->width : screen_w;
	const UINTN draw_h = frame->height <= screen_h ? frame->height : screen_h;
	const UINTN src_x = frame->width > screen_w ? (frame->width - screen_w) / 2 : 0;
	const UINTN src_y = frame->height > screen_h ? (frame->height - screen_h) / 2 : 0;
	const UINTN dst_x = frame->width > screen_w ? 0 : (screen_w - frame->width) / 2;
	const UINTN dst_y = frame->height > screen_h ? 0 : (screen_h - frame->height) / 2;

	if (clear_each_frame) {
		EFI_GRAPHICS_OUTPUT_BLT_PIXEL black = { 0, 0, 0, 0 };
		if (EFI_ERROR(gop->Blt(gop, &black, EfiBltVideoFill, 0, 0, 0, 0, screen_w, screen_h, 0))) {
			return FALSE;
		}
	}
	// One Blt call per frame keeps rendering atomic and reduces tearing/artifacts.
	const UINTN delta = frame->width * sizeof(EFI_GRAPHICS_OUTPUT_BLT_PIXEL);
	return !EFI_ERROR(gop->Blt(
		gop,
		frame->blt,
		EfiBltBufferToVideo,
		src_x, src_y,
		dst_x, dst_y,
		draw_w, draw_h,
		delta
	));
}

static void WaitFrameDurationUs(UINTN frame_duration_us) {
	if (frame_duration_us > 0) {
		BS->Stall(frame_duration_us);
	}
}

static void CleanupPreloadedFrames(struct AnimationBltFrame* frames, UINTN count) {
	if (!frames) {
		return;
	}
	for (UINTN i = 0; i < count; ++i) {
		FreeBltFrame(&frames[i]);
	}
	BS->FreePool(frames);
}

/**
 * Play optional pre-boot animation and optionally return final frame for BGRT.
 */
static BMP* PlayAnimation(EFI_FILE_HANDLE base_dir) {
	if (!config.animation) {
		return 0;
	}
	EFI_GRAPHICS_OUTPUT_PROTOCOL* gop = GOP();
	if (!gop || !gop->Mode || !gop->Mode->Info) {
		Log(config.debug, L"Animation skipped: GOP unavailable.\n");
		return 0;
	}
	if (config.animation_fps <= 0 || config.animation_max_ms <= 0 || config.animation_digits <= 0) {
		Log(1, L"Animation skipped: invalid animation settings.\n");
		return 0;
	}

	Log(config.debug, L"Animation GOP mode: %dx%d, format=%s.\n",
		(int) gop->Mode->Info->HorizontalResolution,
		(int) gop->Mode->Info->VerticalResolution,
		GopPixelFormatName(gop->Mode->Info->PixelFormat)
	);

	int animation_fps = config.animation_fps;
	if (animation_fps <= 0) {
		animation_fps = 15;
	}
	if (animation_fps > 60) {
		animation_fps = 60;
	}
	const UINTN frame_duration_us = max(1, 1000000 / (UINTN) animation_fps);
	UINTN max_frames = ((UINTN) config.animation_max_ms * 1000U) / frame_duration_us;
	if (max_frames == 0) {
		max_frames = 1;
	}
	Log(config.debug, L"Animation timing: fps=%d, frame_us=%d, max_ms=%d.\n",
		animation_fps, (int) frame_duration_us, config.animation_max_ms);

	UINT32 frame_space = 1;
	for (int i = 0; i < config.animation_digits && frame_space <= 100000000; ++i) {
		frame_space *= 10;
	}

	CHAR16 frame_path[512];
	UINT32 start_index = 0;
	BMP* probe = 0;
	if (BuildAnimationFramePath(frame_path, 512, 0)) {
		probe = LoadAnimationBMP(base_dir, frame_path);
	}
	if (!probe && BuildAnimationFramePath(frame_path, 512, 1)) {
		probe = LoadAnimationBMP(base_dir, frame_path);
		start_index = 1;
	}
	if (!probe) {
		Log(config.debug, L"Animation skipped: no frame_000/frame_001 found.\n");
		return 0;
	}
	BS->FreePool(probe);

	struct AnimationBltFrame* preloaded = 0;
	UINTN preloaded_count = 0;
	BOOLEAN preload_enabled = config.animation_preload != 0;
	UINTN preload_budget = (UINTN) max(1, config.animation_max_preload_mb) * 1024U * 1024U;
	UINTN preload_used = 0;
	UINTN detected_frames = 0;
	UINTN detected_limit = frame_space > start_index ? frame_space - start_index : 1;
	if (detected_limit > 10000) {
		detected_limit = 10000;
	}

	if (preload_enabled) {
		if (EFI_ERROR(BS->AllocatePool(EfiBootServicesData, detected_limit * sizeof(*preloaded), (void**)&preloaded))) {
			preload_enabled = FALSE;
			preloaded = 0;
			Log(config.debug, L"Animation preload disabled: frame table allocation failed.\n");
		} else {
			BS->SetMem(preloaded, detected_limit * sizeof(*preloaded), 0);
		}
	}

	for (UINTN n = 0; n < detected_limit; ++n) {
		UINT32 frame_index = start_index + (UINT32) n;
		if (!BuildAnimationFramePath(frame_path, 512, frame_index)) {
			break;
		}
		BMP* bmp = LoadAnimationBMP(base_dir, frame_path);
		if (!bmp) {
			break;
		}

		UINT32 row_size = 0;
		INT32 width = 0;
		INT32 height = 0;
		BOOLEAN top_down = FALSE;
		if (!ValidateBMPData(bmp, bmp->file_size, &row_size, &width, &height, &top_down)) {
			BS->FreePool(bmp);
			break;
		}
		Log(config.debug, L"Animation frame %d: %dx%d, bpp=%d, row=%d, bytes=%d.\n",
			(int) frame_index, width, height, (int) bmp->bpp, (int) row_size, (int) (row_size * height));

		if (preload_enabled) {
			struct AnimationBltFrame decoded = {0};
			if (!DecodeBMPToBltFrame(bmp, &decoded)) {
				Log(config.debug, L"Animation preload stopped: decode failed at frame %d.\n", (int) frame_index);
				preload_enabled = FALSE;
				CleanupPreloadedFrames(preloaded, preloaded_count);
				preloaded = 0;
				preloaded_count = 0;
			} else if (preload_used + decoded.blt_bytes > preload_budget) {
				Log(config.debug, L"Animation preload stopped: memory cap hit at frame %d.\n", (int) frame_index);
				FreeBltFrame(&decoded);
				preload_enabled = FALSE;
				CleanupPreloadedFrames(preloaded, preloaded_count);
				preloaded = 0;
				preloaded_count = 0;
			} else {
				preloaded[preloaded_count++] = decoded;
				preload_used += decoded.blt_bytes;
			}
		}

		BS->FreePool(bmp);
		++detected_frames;
	}

	if (detected_frames == 0) {
		if (preloaded) {
			CleanupPreloadedFrames(preloaded, preloaded_count);
		}
		Log(config.debug, L"Animation skipped: no valid animation frames.\n");
		return 0;
	}

	Log(config.debug, L"Animation detected frame count=%d, preload=%d, preload_mb=%d.\n",
		(int) detected_frames, preloaded ? 1 : 0, (int) (preload_used >> 20));

	UINTN played = 0;
	UINTN frame_slot = 0;
	UINT32 last_index_played = start_index;
	int failed = 0;

	while (played < max_frames) {
		UINTN logical_slot = frame_slot % detected_frames;
		UINT32 current_index = start_index + (UINT32) logical_slot;
		struct AnimationBltFrame frame = {0};
		int frame_needs_free = 0;

		if (preloaded) {
			frame = preloaded[logical_slot];
		} else {
			if (!BuildAnimationFramePath(frame_path, 512, current_index)) {
				failed = 1;
				break;
			}
			BMP* bmp = LoadAnimationBMP(base_dir, frame_path);
			if (!bmp || !DecodeBMPToBltFrame(bmp, &frame)) {
				if (bmp) {
					BS->FreePool(bmp);
				}
				failed = 1;
				break;
			}
			frame_needs_free = 1;
			BS->FreePool(bmp);
		}

		if (frame.width > gop->Mode->Info->HorizontalResolution || frame.height > gop->Mode->Info->VerticalResolution) {
			Log(config.debug, L"Animation frame %d larger than GOP screen, clamping (%dx%d -> %dx%d).\n",
				(int) current_index, (int) frame.width, (int) frame.height,
				(int) gop->Mode->Info->HorizontalResolution, (int) gop->Mode->Info->VerticalResolution);
		}

		if (!DrawBltFrame(gop, &frame, config.animation_clear_each_frame)) {
			if (frame_needs_free) {
				FreeBltFrame(&frame);
			}
			failed = 1;
			break;
		}
		last_index_played = current_index;
		if (frame_needs_free) {
			FreeBltFrame(&frame);
		}

		++played;
		++frame_slot;
		if (played >= max_frames) {
			break;
		}

		// Stall expects microseconds, so frame pacing is controlled by fps precisely.
		WaitFrameDurationUs(frame_duration_us);
	}

	Log(config.debug, L"Animation playback: displayed=%d frame(s), preload=%d.\n", (int) played, preloaded ? 1 : 0);
	if (preloaded) {
		CleanupPreloadedFrames(preloaded, preloaded_count);
	}

	if (!config.animation_final_last || failed) {
		return 0;
	}
	if (!BuildAnimationFramePath(frame_path, 512, last_index_played)) {
		return 0;
	}
	BMP* final_frame = LoadAnimationBMP(base_dir, frame_path);
	if (!final_frame) {
		return 0;
	}
	INT32 width = 0;
	INT32 height = 0;
	BOOLEAN top_down = FALSE;
	if (!ParseBMPGeometry(final_frame, &width, &height, &top_down) || top_down) {
		BS->FreePool(final_frame);
		return 0;
	}
	return final_frame;
}

/**
 * The main logic for BGRT modification.
 *
 * @param base_dir The directory for loading a BMP.
 * @param final_animation_bmp The final animation frame to use for BGRT (or 0).
 */
void HackBgrt(EFI_FILE_HANDLE base_dir, BMP* final_animation_bmp) {
	// REMOVE: simply delete all BGRT entries.
	if (config.image.action == HackBGRT_ACTION_REMOVE) {
		if (final_animation_bmp) {
			BS->FreePool(final_animation_bmp);
		}
		HandleAcpiTables(config.image.action, 0);
		return;
	}

	// KEEP/REPLACE: first get the old BGRT entry.
	ACPI_BGRT* bgrt = HandleAcpiTables(HackBGRT_ACTION_KEEP, 0);

	// Get the old BMP and position (relative to screen center), if possible.
	const int old_valid = bgrt && VerifyAcpiSdtChecksum(bgrt);
	BMP* old_bmp = old_valid ? (BMP*) (UINTN) bgrt->image_address : 0;
	const int old_orientation = old_valid ? ((bgrt->status >> 1) & 3) : 0;
	const int old_swap = old_orientation & 1;
	const int old_reso_x = old_swap ? config.old_resolution_y : config.old_resolution_x;
	const int old_reso_y = old_swap ? config.old_resolution_x : config.old_resolution_y;
	const int old_x = old_bmp ? bgrt->image_offset_x + (old_bmp->width - old_reso_x) / 2 : 0;
	const int old_y = old_bmp ? bgrt->image_offset_y + (old_bmp->height - old_reso_y) / 2 : 0;

	// Missing BGRT?
	if (!bgrt) {
		// Keep missing = do nothing.
		if (config.image.action == HackBGRT_ACTION_KEEP) {
			return;
		}
		// Replace missing = allocate new.
		BS->AllocatePool(EfiACPIReclaimMemory, sizeof(*bgrt), (void**)&bgrt);
		if (!bgrt) {
			Log(1, L"Failed to allocate memory for BGRT.\n");
			return;
		}
	}

	*bgrt = (ACPI_BGRT) {
		.header = {
			.signature = "BGRT",
			.length = sizeof(*bgrt),
			.revision = 1,
			.oem_id = "Mtblx*",
			.oem_table_id = "HackBGRT",
			.oem_revision = 1,
			.asl_compiler_id = *(const UINT32*) "None",
			.asl_compiler_revision = 1,
		},
		.version = 1,
	};

	// Get the image (either old or new).
	BMP* new_bmp = old_bmp;
	if (config.image.action == HackBGRT_ACTION_REPLACE) {
		new_bmp = final_animation_bmp ? final_animation_bmp : LoadBMP(base_dir, config.image.path);
	} else if (final_animation_bmp) {
		BS->FreePool(final_animation_bmp);
	}

	// No image = no need for BGRT.
	if (!new_bmp) {
		HandleAcpiTables(HackBGRT_ACTION_REMOVE, 0);
		return;
	}

	// Crop the image to screen.
	CropBMP(new_bmp, config.resolution_x, config.resolution_y);

	// Set the image address and orientation.
	bgrt->image_address = (UINTN) new_bmp;
	const int new_orientation = config.image.orientation == HackBGRT_ORIENTATION_KEEP ? old_orientation : config.image.orientation;
	bgrt->status = new_orientation << 1;

	// New center coordinates.
	const int new_swap = new_orientation & 1;
	const int new_reso_x = new_swap ? config.resolution_y : config.resolution_x;
	const int new_reso_y = new_swap ? config.resolution_x : config.resolution_y;

	const int new_x =
		config.image.x_mode == HackBGRT_COORDINATE_MODE_KEEP ? old_x :
		config.image.x_mode == HackBGRT_COORDINATE_MODE_CENTERED ? config.image.x :
		(config.image.x - HackBGRT_FRACTION_HALF) * new_reso_x / HackBGRT_FRACTION_ONE;
	const int new_y =
		config.image.y_mode == HackBGRT_COORDINATE_MODE_KEEP ? old_y :
		config.image.y_mode == HackBGRT_COORDINATE_MODE_CENTERED ? config.image.y :
		(config.image.y - HackBGRT_FRACTION_HALF) * new_reso_y / HackBGRT_FRACTION_ONE;

	// Calculate absolute position.
	const int max_x = new_reso_x - new_bmp->width;
	const int max_y = new_reso_y - new_bmp->height;
	bgrt->image_offset_x = max(0, min(max_x, new_x + (new_reso_x - new_bmp->width) / 2));
	bgrt->image_offset_y = max(0, min(max_y, new_y + (new_reso_y - new_bmp->height) / 2));

	Log(config.debug,
		L"Screen %dx%d, BMP %dx%d, center (%d, %d) = corner (%d, %d), orientation %d.\n",
		new_reso_x, new_reso_y,
		new_bmp->width, new_bmp->height,
		new_x, new_y,
		(int) bgrt->image_offset_x, (int) bgrt->image_offset_y,
		new_orientation * 90
	);

	// Store this BGRT in the ACPI tables.
	SetAcpiSdtChecksum(bgrt);
	HandleAcpiTables(HackBGRT_ACTION_REPLACE, bgrt);
}

/**
 * Load an application.
 */
static EFI_HANDLE LoadApp(int print_failure, EFI_HANDLE image_handle, EFI_LOADED_IMAGE* image, const CHAR16* path) {
	EFI_DEVICE_PATH* boot_dp = FileDevicePath(image->DeviceHandle, (CHAR16*) path);
	EFI_HANDLE result = 0;
	Log(config.debug, L"Loading application %s.\n", path);
	if (EFI_ERROR(BS->LoadImage(0, image_handle, boot_dp, 0, 0, &result))) {
		Log(config.debug || print_failure, L"Failed to load application %s.\n", path);
	}
	return result;
}

/**
 * The main program.
 */
EFI_STATUS EFIAPI efi_main(EFI_HANDLE image_handle, EFI_SYSTEM_TABLE *ST_) {
	ST = ST_;
	BS = ST_->BootServices;
	RT = ST_->RuntimeServices;

	// Clear the screen to wipe the vendor logo.
	ST->ConOut->EnableCursor(ST->ConOut, 0);
	ST->ConOut->ClearScreen(ST->ConOut);

	Log(0, L"HackBGRT version: %s\n", version);

	EFI_LOADED_IMAGE* image;
	if (EFI_ERROR(BS->HandleProtocol(image_handle, TmpGuidPtr((EFI_GUID) EFI_LOADED_IMAGE_PROTOCOL_GUID), (void**) &image))) {
		Log(config.debug, L"LOADED_IMAGE_PROTOCOL failed.\n");
		goto fail;
	}

	EFI_FILE_IO_INTERFACE* io;
	if (EFI_ERROR(BS->HandleProtocol(image->DeviceHandle, TmpGuidPtr((EFI_GUID) EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID), (void**) &io))) {
		Log(config.debug, L"FILE_SYSTEM_PROTOCOL failed.\n");
		goto fail;
	}

	EFI_FILE_HANDLE root_dir;
	if (EFI_ERROR(io->OpenVolume(io, &root_dir))) {
		Log(config.debug, L"Failed to open root directory.\n");
		goto fail;
	}

	CHAR16* default_dir_path = L"\\EFI\\HackBGRT";
	Log(config.debug, L"Default directory: %s\n", default_dir_path);
	EFI_FILE_HANDLE default_dir;
	if (EFI_ERROR(root_dir->Open(root_dir, &default_dir, default_dir_path, EFI_FILE_MODE_READ, 0))) {
		Log(config.debug, L"Failed to open HackBGRT default directory.\n");
		default_dir = root_dir;
	}

	CHAR16* working_dir_path = DevicePathToStr(image->FilePath);
	for (int i = StrLen(working_dir_path), skipped_last_component = 0; i--;) {
		if (working_dir_path[i] == L'/' || working_dir_path[i] == L'\\') {
			working_dir_path[i] = skipped_last_component++ ? L'\\' : L'\0';
		}
	}
	Log(config.debug, L"Working directory: %s\n", working_dir_path);
	EFI_FILE_HANDLE working_dir;
	if (EFI_ERROR(root_dir->Open(root_dir, &working_dir, working_dir_path, EFI_FILE_MODE_READ, 0))) {
		Log(config.debug, L"Failed to open HackBGRT working directory.\n");
		working_dir = default_dir;
	}

	EFI_FILE_HANDLE base_dir = working_dir;

	EFI_SHELL_PARAMETERS_PROTOCOL *shell_param_proto = NULL;
	if (EFI_ERROR(BS->OpenProtocol(image_handle, TmpGuidPtr((EFI_GUID) EFI_SHELL_PARAMETERS_PROTOCOL_GUID), (void**) &shell_param_proto, 0, 0, EFI_OPEN_PROTOCOL_GET_PROTOCOL)) || shell_param_proto->Argc <= 1) {
		const CHAR16* config_path = L"config.txt";
		retry_read_config:
		if (!ReadConfigFile(&config, base_dir, config_path)) {
			if (base_dir != default_dir && StrCmp(default_dir_path, working_dir_path) != 0) {
				base_dir = default_dir;
				Log(config.debug, L"Trying the default directory.\n");
				goto retry_read_config;
			}
			Log(1, L"No config, no command line!\n", config_path);
			goto fail;
		}
	} else {
		CHAR16 **argv = shell_param_proto->Argv;
		int argc = shell_param_proto->Argc;
		for (int i = 1; i < argc; ++i) {
			ReadConfigLine(&config, base_dir, argv[i]);
		}
	}

	if (config.debug) {
		Log(-1, L"HackBGRT version: %s\n", version);
	}

	SetResolution(config.resolution_x, config.resolution_y);
	BMP* final_animation_bmp = PlayAnimation(base_dir);
	HackBgrt(base_dir, final_animation_bmp);

	EFI_HANDLE next_image_handle = 0;
	static CHAR16 backup_boot_path[] = L"\\EFI\\HackBGRT\\bootmgfw-original.efi";
	static CHAR16 ms_boot_path[] = L"\\EFI\\Microsoft\\Boot\\bootmgfw.efi";
	int try_ms_quietly = 1;

	if (config.boot_path && StriCmp(config.boot_path, L"MS") != 0) {
		next_image_handle = LoadApp(1, image_handle, image, config.boot_path);
		try_ms_quietly = 0;
	}
	if (!next_image_handle) {
		config.boot_path = backup_boot_path;
		next_image_handle = LoadApp(!try_ms_quietly, image_handle, image, config.boot_path);
		if (!next_image_handle) {
			config.boot_path = ms_boot_path;
			next_image_handle = LoadApp(!try_ms_quietly, image_handle, image, config.boot_path);
			if (!next_image_handle) {
				goto fail;
			}
		}
		if (try_ms_quietly) {
			goto ready_to_boot;
		}
		Log(1, L"Reverting to %s.\n", config.boot_path);
		Log(-1, L"Press escape to cancel or any other key (or wait 15 seconds) to boot.\n");
		if (ReadKey(15000).ScanCode == SCAN_ESC) {
			goto fail;
		}
	} else ready_to_boot: if (config.debug) {
		Log(-1, L"Ready to boot.\n");
		Log(-1, L"If all goes well, you can set debug=0 and log=0 in config.txt.\n");
		Log(-1, L"Press escape to cancel or any other key (or wait 15 seconds) to boot.\n");
		if (ReadKey(15000).ScanCode == SCAN_ESC) {
			return 0;
		}
	}
	if (!config.log) {
		ClearLogVariable();
	}
	if (EFI_ERROR(BS->StartImage(next_image_handle, 0, 0))) {
		Log(1, L"Failed to start %s.\n", config.boot_path);
		goto fail;
	}
	Log(1, L"Started %s. Why are we still here?!\n", config.boot_path);
	Log(-1, L"Please check that %s is not actually HackBGRT!\n", config.boot_path);
	goto fail;

	fail: {
		Log(1, L"HackBGRT has failed.\n");
		Log(-1, L"Dumping log:\n\n");
		DumpLog();
		Log(-1, L"If you can't boot into Windows, get install/recovery disk to fix your boot.\n");
		Log(-1, L"Press any key (or wait 15 seconds) to exit.\n");
		ReadKey(15000);
		return 1;
	}
}
