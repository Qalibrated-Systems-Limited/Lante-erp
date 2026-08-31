using AutoMapper;
using TicketingService.Core.DTOs.Tags;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class TagService(ITagRepository tagRepository, IMapper mapper) : ITagService
{
    public async Task<IEnumerable<TagReadDto>> GetAllAsync()
    {
        var tags = await tagRepository.GetAllAsync();
        return mapper.Map<IEnumerable<TagReadDto>>(tags);
    }

    public async Task<TagReadDto?> GetByIdAsync(string id)
    {
        var tag = await tagRepository.GetByIdAsync(id);
        return tag == null ? null : mapper.Map<TagReadDto>(tag);
    }

    public async Task<TagReadDto> CreateAsync(CreateTagDto dto, string createdByUserId)
    {
        var existing = await tagRepository.GetByNameAsync(dto.Name);
        if (existing != null)
            throw new InvalidOperationException($"A tag with name '{dto.Name}' already exists.");

        var tag = mapper.Map<Tag>(dto);
        tag.CreatedBy = createdByUserId;
        tag.UpdatedBy = createdByUserId;

        var created = await tagRepository.CreateAsync(tag);
        return mapper.Map<TagReadDto>(created);
    }

    public async Task<TagReadDto> UpdateAsync(string id, UpdateTagDto dto, string updatedByUserId)
    {
        var tag = await tagRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Tag {id} not found.");

        if (dto.Name != null) tag.Name = dto.Name;
        if (dto.Color != null) tag.Color = dto.Color;
        if (dto.Description != null) tag.Description = dto.Description;
        tag.UpdatedBy = updatedByUserId;

        var updated = await tagRepository.UpdateAsync(tag);
        return mapper.Map<TagReadDto>(updated);
    }

    public Task<bool> DeleteAsync(string id) => tagRepository.DeleteAsync(id);

    public async Task<IEnumerable<TagReadDto>> GetTicketTagsAsync(string ticketId)
    {
        var ticketTags = await tagRepository.GetTicketTagsAsync(ticketId);
        return ticketTags.Select(tt => mapper.Map<TagReadDto>(tt.Tag));
    }

    public async Task AddTagToTicketAsync(string ticketId, string tagId, string addedByUserId)
    {
        var existing = await tagRepository.GetTicketTagAsync(ticketId, tagId);
        if (existing != null) return; // already tagged

        var ticketTag = new TicketTag
        {
            TicketId = ticketId,
            TagId = tagId,
            AddedByUserId = addedByUserId,
            CreatedBy = addedByUserId
        };
        await tagRepository.AddTagToTicketAsync(ticketTag);
    }

    public Task RemoveTagFromTicketAsync(string ticketId, string tagId) =>
        tagRepository.RemoveTagFromTicketAsync(ticketId, tagId);
}
